using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using Spine.Unity;
using UnityEngine;

using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
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
                "Assets/FPGDemo/Presentation/Characters/Players/Fei/Prefabs/PF_FPG_FeiEntity.prefab");
            Assert.That(player.TryValidate(out string error), Is.True, error);

            string[] enemyPaths =
            {
                "Assets/FPGDemo/Presentation/Characters/Enemies/Burstbug/Prefabs/PF_FPG_BurstbugEntity.prefab",
                "Assets/FPGDemo/Presentation/Characters/Enemies/Luan/Prefabs/PF_FPG_LuanEntity.prefab",
                "Assets/FPGDemo/Presentation/Characters/Enemies/Hudie/Prefabs/PF_FPG_HudieEntity.prefab"
            };
            for (int index = 0; index < enemyPaths.Length; index++)
            {
                FpgEnemyEntityView enemy = LoadEntity<FpgEnemyEntityView>(enemyPaths[index]);
                Assert.That(enemy.TryValidate(out error), Is.True, enemyPaths[index] + ": " + error);
            }
        }


        [Test]
        public void PlayerFacingExecutionOrderFollowsReticleBeforeAimSampling()
        {
            DefaultExecutionOrder reticleOrder =
                typeof(CombatAimReticle).GetCustomAttribute<
                    DefaultExecutionOrder>();
            DefaultExecutionOrder facingOrder =
                typeof(FpgPlayerFacingController).GetCustomAttribute<
                    DefaultExecutionOrder>();

            Assert.That(reticleOrder, Is.Not.Null);
            Assert.That(facingOrder, Is.Not.Null);
            Assert.That(reticleOrder.order, Is.EqualTo(-500));
            Assert.That(facingOrder.order, Is.EqualTo(-400));
            Assert.That(facingOrder.order, Is.LessThan(0));
        }


        [Test]
        public void FormalEnemyHitboxFollowOffsetsRemainFinite()
        {
            string[] enemyPaths =
            {
                "Assets/FPGDemo/Presentation/Characters/Enemies/Burstbug/Prefabs/PF_FPG_BurstbugEntity.prefab",
                "Assets/FPGDemo/Presentation/Characters/Enemies/Luan/Prefabs/PF_FPG_LuanEntity.prefab",
                "Assets/FPGDemo/Presentation/Characters/Enemies/Hudie/Prefabs/PF_FPG_HudieEntity.prefab"
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
                    Assert.That(settings.HasFiniteOffsets, Is.True);
                }
            }
        }

        [Test]
        public void FormalPlayerAndIndependentCoverPrefabsSatisfyContracts()
        {
            FpgPlayerEntityView player = LoadEntity<FpgPlayerEntityView>(
                "Assets/FPGDemo/Presentation/Characters/Players/Fei/Prefabs/PF_FPG_FeiEntity.prefab");
            FpgPlayerBarrierPresentationController cover = player.Barrier;

            Assert.That(cover, Is.Not.Null);
            Assert.That(cover.transform, Is.SameAs(player.transform));
            Assert.That(cover.PeekRoot.parent, Is.SameAs(player.transform));
            Assert.That(player.FacingController, Is.Not.Null);
            Assert.That(player.FacingController.transform, Is.SameAs(player.transform));
            Assert.That(player.FacingRoot.parent, Is.SameAs(cover.PeekRoot));
            Assert.That(player.FacingRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(player.FacingRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(player.FacingRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(player.FacingRoot.childCount, Is.EqualTo(2));
            Assert.That(player.VisualRoot.parent, Is.SameAs(player.FacingRoot));
            Assert.That(
                cover.PrimaryPresentationMuzzle.parent.parent,
                Is.SameAs(player.FacingRoot));
            Assert.That(
                cover.SecondaryPresentationMuzzle.parent,
                Is.SameAs(cover.PrimaryPresentationMuzzle.parent));
            Assert.That(cover.CoverVisualRoot, Is.Null);
            Assert.That(cover.CoverRenderer, Is.Null);
            Assert.That(player.transform.Find("CoverRoot"), Is.Null);
            Assert.That(player.transform.Find("CoverWall"), Is.Null);
            Assert.That(cover.HasSelectedPeekTarget, Is.False);
            Assert.That(cover.CurrentPeekLocalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(
                new SerializedObject(cover).FindProperty("peekLocalOffset"),
                Is.Null);
            Assert.That(
                cover.PrimaryPresentationMuzzle.IsChildOf(cover.PeekRoot),
                Is.True);
            Assert.That(
                cover.SecondaryPresentationMuzzle.IsChildOf(cover.PeekRoot),
                Is.True);
            Assert.That(
                player.SocketRegistry.transform.IsChildOf(cover.PeekRoot),
                Is.False);

            FpgCoverTraversalPresenter traversal =
                player.GetComponent<FpgCoverTraversalPresenter>();
            Assert.That(traversal, Is.Not.Null);
            SerializedObject traversalSo = new SerializedObject(traversal);
            FpgCoverTransitionEffectView effect = traversalSo
                .FindProperty("transitionEffectPrefab")
                .objectReferenceValue as FpgCoverTransitionEffectView;
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.TryValidate(out string effectError), Is.True, effectError);
            Assert.That(
                AssetDatabase.GetAssetPath(effect.gameObject),
                Is.EqualTo(
                    "Assets/FPGDemo/Presentation/Level/Covers/VFX/PF_FPG_CoverTransition.prefab"));

            AssertCoverPrefabContract(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_DefaultCover.prefab");
            GameObject treeCover = AssertCoverPrefabContract(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_Root1TreeCover.prefab");
            AssertCoverHealthStages(
                treeCover,
                new[] { 67, 34, 1 },
                new[]
                {
                    "HealthStage_100",
                    "HealthStage_66",
                    "HealthStage_33"
                },
                new[]
                {
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_tree1_block.png",
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_tree1_block_66.png",
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_tree1_block_33.png"
                },
                "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_tree1_block_0.png");

            GameObject boatLeft = AssertCoverPrefabContract(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_BoatLeft.prefab");
            AssertCoverHealthStages(
                boatLeft,
                new[] { 51, 1 },
                new[] { "HealthStage_100", "HealthStage_50" },
                new[]
                {
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_boat_block_L_100_.png",
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_boat_block_L_50.png"
                },
                "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_boat_block_L_0.png");

            GameObject boatRight = AssertCoverPrefabContract(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_BoatRight.prefab");
            AssertCoverHealthStages(
                boatRight,
                new[] { 51, 1 },
                new[] { "HealthStage_100", "HealthStage_50" },
                new[]
                {
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_boat_block_R_100_.png",
                    "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_boat_block_R_50.png"
                },
                "Assets/FPGDemo/Presentation/Level/Environment/rootArt/root1/root1_boat_block_R_0_.png");
        }

        [Test]
        public void CoverPrefabValidationRejectsInvalidOwnershipAndTriggerBlockers()
        {
            GameObject root = CreateObject("CoverRoot");
            FpgCoverEntityView view = root.AddComponent<FpgCoverEntityView>();
            GameObject intact = CreateChild(root.transform, "Intact").gameObject;
            GameObject destroyed = CreateChild(root.transform, "Destroyed").gameObject;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(visual);
            visual.name = "VisualMesh";
            visual.transform.SetParent(intact.transform, false);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
            MeshCollider blocker = visual.AddComponent<MeshCollider>();
            blocker.sharedMesh = meshFilter.sharedMesh;
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("intactRoot").objectReferenceValue = intact;
            serialized.FindProperty("destroyedRoot").objectReferenceValue = destroyed;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(view.TryValidate(out string error), Is.True, error);

            serialized.Update();
            serialized.FindProperty("destroyedRoot").objectReferenceValue = intact;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(view.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("distinct"));

            serialized.Update();
            serialized.FindProperty("intactRoot").objectReferenceValue = root;
            serialized.FindProperty("destroyedRoot").objectReferenceValue = destroyed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(view.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("belong"));

            GameObject external = CreateObject("ExternalVisualRoot");
            serialized.Update();
            serialized.FindProperty("intactRoot").objectReferenceValue = intact;
            serialized.FindProperty("destroyedRoot").objectReferenceValue = external;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(view.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("belong"));

            serialized.Update();
            serialized.FindProperty("destroyedRoot").objectReferenceValue = destroyed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            blocker.convex = true;
            blocker.isTrigger = true;
            Assert.That(view.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("Trigger"));

            blocker.isTrigger = false;
            blocker.convex = false;
            blocker.sharedMesh = null;
            Assert.That(view.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("MeshFilter mesh"));
        }

        [Test]
        public void CoverBlockersPreferShadowProxyOverRenderableVisualMeshes()
        {
            GameObject root = CreateObject("CoverRoot");
            FpgCoverEntityView view = root.AddComponent<FpgCoverEntityView>();
            GameObject intact = CreateChild(root.transform, "Intact").gameObject;
            GameObject destroyed = CreateChild(root.transform, "Destroyed").gameObject;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(visual);
            visual.name = "VisualMesh";
            visual.transform.SetParent(intact.transform, false);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            GameObject shadowProxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(shadowProxy);
            shadowProxy.name = "__ShadowCasterProxy";
            shadowProxy.transform.SetParent(intact.transform, false);
            UnityEngine.Object.DestroyImmediate(
                shadowProxy.GetComponent<BoxCollider>());
            MeshFilter shadowMesh = shadowProxy.GetComponent<MeshFilter>();
            MeshCollider shadowCollider = shadowProxy.AddComponent<MeshCollider>();
            shadowCollider.sharedMesh = shadowMesh.sharedMesh;

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("intactRoot").objectReferenceValue = intact;
            serialized.FindProperty("destroyedRoot").objectReferenceValue = destroyed;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(view.TryValidate(out string error), Is.True, error);
            Assert.That(view.BlockingColliderCount, Is.EqualTo(1));
            Assert.That(
                view.TryGetBlockingCollider(0, out Collider blocker),
                Is.True);
            Assert.That(blocker, Is.SameAs(shadowCollider));

            shadowMesh.sharedMesh = null;
            Assert.That(view.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("non-empty shared Mesh"));
        }

        [Test]
        public void CoverBlockersIncludeEveryRenderableVisualMeshWithoutProxy()
        {
            GameObject root = CreateObject("CoverRoot");
            FpgCoverEntityView view = root.AddComponent<FpgCoverEntityView>();
            GameObject intact = CreateChild(root.transform, "Intact").gameObject;
            GameObject destroyed = CreateChild(root.transform, "Destroyed").gameObject;
            MeshCollider[] expected = new MeshCollider[2];
            for (int index = 0; index < expected.Length; index++)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                createdObjects.Add(visual);
                visual.name = $"VisualMesh_{index}";
                visual.transform.SetParent(intact.transform, false);
                UnityEngine.Object.DestroyImmediate(
                    visual.GetComponent<BoxCollider>());
                MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
                expected[index] = visual.AddComponent<MeshCollider>();
                expected[index].sharedMesh = meshFilter.sharedMesh;
            }

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("intactRoot").objectReferenceValue = intact;
            serialized.FindProperty("destroyedRoot").objectReferenceValue = destroyed;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(view.TryValidate(out string error), Is.True, error);
            Assert.That(view.BlockingColliderCount, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(
                    view.TryGetBlockingCollider(index, out Collider blocker),
                    Is.True);
                Assert.That(blocker, Is.SameAs(expected[index]));
            }
        }

        [Test]
        public void FormalCoverHealthStagesApplySnapshotsAndToggleColliders()
        {
            AssertCoverStageSnapshots(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_Root1TreeCover.prefab",
                new[] { 100, 66, 33 },
                new[] { 0, 1, 2 });
            AssertCoverStageSnapshots(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_BoatLeft.prefab",
                new[] { 100, 50 },
                new[] { 0, 1 });
            AssertCoverStageSnapshots(
                "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_BoatRight.prefab",
                new[] { 100, 50 },
                new[] { 0, 1 });
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
                "Assets/FPGDemo/Presentation/Characters/Enemies/Hudie/Prefabs/PF_FPG_HudieEntity.prefab";
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
            Assert.That(
                offset.TryGetHitPartFollowSettings(
                    0,
                    out D0EnemyHitboxFollowSettings authoredFollow),
                Is.True);
            var serializedOffset = new SerializedObject(offset);
            Vector3 extraOffset = new Vector3(0f, 0f, 0.4f);
            serializedOffset.FindProperty("hitPartFollowSettings")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("positionOffset")
                .vector3Value = authoredFollow.PositionOffset + extraOffset;
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
                Is.SameAs(player.ShotOrigin));
            Assert.That(presentationMuzzle, Is.SameAs(authoritativeMuzzle));
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
        public void PlayerFacingYawKeepsSpineShotOriginFiniteAtSampleAngles()
        {
            const string path =
                "Assets/FPGDemo/Presentation/Characters/Players/Fei/Prefabs/PF_FPG_FeiEntity.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            createdObjects.Add(instance);

            FpgPlayerEntityView player =
                instance.GetComponent<FpgPlayerEntityView>();
            Assert.That(player, Is.Not.Null);
            player.SkeletonAnimation.Initialize(false);
            Assert.That(
                player.TryBindSpineSocketFollowers(out string bindError),
                Is.True,
                bindError);

            float[] sampleAngles = { 0f, 45f, 90f, 135f, 180f };
            for (int index = 0; index < sampleAngles.Length; index++)
            {
                player.FacingRoot.localRotation =
                    Quaternion.AngleAxis(sampleAngles[index], Vector3.up);
                player.SkeletonAnimation.Skeleton.SetToSetupPose();
                player.SkeletonAnimation.Skeleton.UpdateWorldTransform();
                Assert.That(
                    player.TryRefreshSpineSocketFollowers(
                        out string refreshError),
                    Is.True,
                    sampleAngles[index] + ": " + refreshError);
                AssertFinite(player.ShotOrigin.position, sampleAngles[index]);
                AssertFinite(player.ShotOrigin.rotation, sampleAngles[index]);
            }
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
            Transform facingRoot = CreateChild(peekRoot, "FacingRoot");
            Transform visual = CreateChild(facingRoot, "VisualRoot");
            Transform presentationSockets = CreateChild(
                facingRoot,
                "PresentationSockets");
            Transform primaryPresentationMuzzle = CreateChild(
                presentationSockets,
                "PrimaryPresentationMuzzle");
            Transform secondaryPresentationMuzzle = CreateChild(
                presentationSockets,
                "SecondaryPresentationMuzzle");
            FpgPlayerBarrierPresentationController barrier =
                root.AddComponent<
                    FpgPlayerBarrierPresentationController>();
            FpgPlayerFacingController facing =
                root.AddComponent<FpgPlayerFacingController>();
            Transform socketsRoot = CreateChild(root.transform, "Sockets");
            D0ActorSocketRegistry sockets = socketsRoot.gameObject.AddComponent<D0ActorSocketRegistry>();
            Transform primaryMuzzle = CreateChild(socketsRoot, "PrimaryMuzzle");
            Transform secondaryMuzzle = CreateChild(socketsRoot, "SecondaryMuzzle");
            Transform attackOrigin = CreateChild(socketsRoot, "AttackOrigin");
            Actor2DPresenter presenter = root.AddComponent<Actor2DPresenter>();
            SkeletonAnimation skeleton =
                visual.gameObject.AddComponent<SkeletonAnimation>();
            FpgPlayerEntityView authoredPlayer =
                LoadEntity<FpgPlayerEntityView>(
                    "Assets/FPGDemo/Presentation/Characters/Players/Fei/Prefabs/PF_FPG_FeiEntity.prefab");
            skeleton.skeletonDataAsset =
                authoredPlayer.SkeletonAnimation.skeletonDataAsset;
            skeleton.Initialize(false);
            Transform aim = CreateChild(root.transform, "AimAnchor");
            Transform shotOrigin = primaryMuzzle;
            Transform ground = CreateChild(root.transform, "GroundAnchor");
            Transform camera = CreateChild(root.transform, "CameraPivot");
            Transform bodyTransform = CreateChild(gameplay, "BodyHitbox");
            BoxCollider body = bodyTransform.gameObject.AddComponent<BoxCollider>();

            Assert.That(
                sockets.TryRegister(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    shotOrigin,
                    D0ActorSocketFollowMode.SpineBone,
                    "l_hand",
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

            SetField(barrier, "peekRoot", peekRoot);
            SetField(
                barrier,
                "primaryPresentationMuzzle",
                primaryPresentationMuzzle);
            SetField(
                barrier,
                "secondaryPresentationMuzzle",
                secondaryPresentationMuzzle);
            barrier.ResetPresentation();

            SetField(player, "gameplayAnchor", gameplay);
            SetField(player, "visualRoot", visual);
            SetField(player, "socketRegistry", sockets);
            SetField(player, "actorPresenter", presenter);
            SetField(player, "skeletonAnimation", skeleton);
            SetField(player, "characterController", characterController);
            SetField(player, "bounds", bounds);
            SetField(player, "aimAnchor", aim);
            SetField(player, "shotOrigin", shotOrigin);
            SetField(player, "groundAnchor", ground);
            SetField(player, "cameraPivot", camera);
            SetField(player, "bodyHitbox", body);
            SetField(player, "barrier", barrier);
            SetField(facing, "facingRoot", facingRoot);
            SetField(player, "facingController", facing);
            return player;
        }


        private static void AssertFinite(Vector3 value, float angle)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False, angle.ToString());
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False, angle.ToString());
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False, angle.ToString());
        }

        private static void AssertFinite(Quaternion value, float angle)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False, angle.ToString());
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False, angle.ToString());
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False, angle.ToString());
            Assert.That(float.IsNaN(value.w) || float.IsInfinity(value.w), Is.False, angle.ToString());
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

        private static GameObject AssertCoverPrefabContract(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.layer, Is.EqualTo(28), path);
            FpgCoverEntityView view = prefab.GetComponent<FpgCoverEntityView>();
            Assert.That(view, Is.Not.Null, path);
            Assert.That(view.TryValidate(out string error), Is.True, path + ": " + error);

            SerializedObject serialized = new SerializedObject(view);
            Assert.That(
                serialized.FindProperty("intactRoot").objectReferenceValue,
                Is.Not.Null,
                path);
            Assert.That(
                serialized.FindProperty("destroyedRoot").objectReferenceValue,
                Is.Not.Null,
                path);
            Assert.That(view.BlockingColliderCount, Is.GreaterThan(0), path);
            for (int index = 0; index < view.BlockingColliderCount; index++)
            {
                Assert.That(
                    view.TryGetBlockingCollider(index, out Collider collider),
                    Is.True,
                    path);
                MeshCollider meshCollider = collider as MeshCollider;
                Assert.That(meshCollider, Is.Not.Null, path);
                MeshFilter meshFilter = collider.GetComponent<MeshFilter>();
                Assert.That(meshFilter, Is.Not.Null, path);
                Assert.That(meshCollider.sharedMesh, Is.SameAs(meshFilter.sharedMesh), path);
            }

            return prefab;
        }

        private static void AssertCoverHealthStages(
            GameObject prefab,
            int[] expectedThresholds,
            string[] expectedStageNames,
            string[] expectedStageSpritePaths,
            string expectedDestroyedSpritePath)
        {
            FpgCoverEntityView view = prefab.GetComponent<FpgCoverEntityView>();
            SerializedObject serialized = new SerializedObject(view);
            GameObject intactRoot = serialized.FindProperty("intactRoot")
                .objectReferenceValue as GameObject;
            GameObject destroyedRoot = serialized.FindProperty("destroyedRoot")
                .objectReferenceValue as GameObject;
            Assert.That(intactRoot, Is.Not.Null, prefab.name);
            Assert.That(destroyedRoot, Is.Not.Null, prefab.name);
            Assert.That(intactRoot.name, Is.EqualTo("IntactRoot"));
            Assert.That(destroyedRoot.name, Is.EqualTo("DestroyedRoot"));

            SerializedProperty stages = serialized.FindProperty("healthStages");
            Assert.That(stages.arraySize, Is.EqualTo(expectedThresholds.Length));
            Assert.That(view.HealthStageCount, Is.EqualTo(expectedThresholds.Length));
            for (int index = 0; index < expectedThresholds.Length; index++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(index);
                Assert.That(
                    stage.FindPropertyRelative(
                        "minDurabilityPercentInclusive").intValue,
                    Is.EqualTo(expectedThresholds[index]),
                    prefab.name);
                GameObject visualRoot = stage.FindPropertyRelative("visualRoot")
                    .objectReferenceValue as GameObject;
                Assert.That(visualRoot, Is.Not.Null, prefab.name);
                Assert.That(
                    visualRoot.name,
                    Is.EqualTo(expectedStageNames[index]),
                    prefab.name);
                Assert.That(
                    visualRoot.transform.IsChildOf(intactRoot.transform),
                    Is.True,
                    prefab.name);
                Assert.That(
                    ContainsSpritePath(
                        visualRoot.transform,
                        expectedStageSpritePaths[index]),
                    Is.True,
                    prefab.name);

                MeshCollider[] colliders =
                    visualRoot.GetComponentsInChildren<MeshCollider>(true);
                Assert.That(colliders, Has.Length.EqualTo(1), prefab.name);
                MeshFilter meshFilter =
                    colliders[0].GetComponent<MeshFilter>();
                Assert.That(meshFilter, Is.Not.Null, prefab.name);
                Assert.That(meshFilter.name, Is.EqualTo("__ShadowCasterProxy"));
                Assert.That(meshFilter.sharedMesh, Is.Not.Null, prefab.name);
                Assert.That(
                    colliders[0].sharedMesh,
                    Is.SameAs(meshFilter.sharedMesh),
                    prefab.name);
                Assert.That(colliders[0].isTrigger, Is.False, prefab.name);
                Assert.That(colliders[0].convex, Is.False, prefab.name);
            }

            Assert.That(
                ContainsSpritePath(
                    destroyedRoot.transform,
                    expectedDestroyedSpritePath),
                Is.True,
                prefab.name);
            Assert.That(
                destroyedRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                prefab.name);
        }

        private void AssertCoverStageSnapshots(
            string prefabPath,
            int[] durabilities,
            int[] expectedStageIndices)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null, prefabPath);
            createdObjects.Add(instance);
            FpgCoverEntityView view =
                instance.GetComponent<FpgCoverEntityView>();
            Assert.That(view, Is.Not.Null, prefabPath);

            for (int index = 0; index < durabilities.Length; index++)
            {
                view.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-test",
                    0,
                    durabilities[index],
                    100,
                    false,
                    false,
                    false));
                AssertActiveCoverStage(
                    view,
                    expectedStageIndices[index],
                    prefabPath + ":" + durabilities[index]);
            }

            view.ApplySnapshot(new FpgCoverSnapshot(
                "cover-test",
                0,
                0,
                100,
                false,
                false,
                false));
            AssertActiveCoverStage(view, -1, prefabPath + ":destroyed");
        }

        private static void AssertActiveCoverStage(
            FpgCoverEntityView view,
            int expectedActiveStageIndex,
            string context)
        {
            SerializedObject serialized = new SerializedObject(view);
            GameObject destroyedRoot = serialized.FindProperty("destroyedRoot")
                .objectReferenceValue as GameObject;
            SerializedProperty stages = serialized.FindProperty("healthStages");
            Assert.That(
                view.ActiveHealthStageIndex,
                Is.EqualTo(expectedActiveStageIndex),
                context);
            Assert.That(
                view.IsDestroyed,
                Is.EqualTo(expectedActiveStageIndex < 0),
                context);
            Assert.That(
                destroyedRoot.activeSelf,
                Is.EqualTo(expectedActiveStageIndex < 0),
                context);

            Transform activeRoot = null;
            for (int stageIndex = 0; stageIndex < stages.arraySize; stageIndex++)
            {
                GameObject stageRoot = stages.GetArrayElementAtIndex(stageIndex)
                    .FindPropertyRelative("visualRoot")
                    .objectReferenceValue as GameObject;
                Assert.That(stageRoot, Is.Not.Null, context);
                bool isActive = stageIndex == expectedActiveStageIndex;
                Assert.That(stageRoot.activeSelf, Is.EqualTo(isActive), context);
                if (isActive)
                {
                    activeRoot = stageRoot.transform;
                }
            }

            for (int colliderIndex = 0;
                colliderIndex < view.BlockingColliderCount;
                colliderIndex++)
            {
                Assert.That(
                    view.TryGetBlockingCollider(
                        colliderIndex,
                        out Collider collider),
                    Is.True,
                    context);
                bool shouldBeEnabled = expectedActiveStageIndex >= 0
                    && collider.transform.IsChildOf(activeRoot);
                Assert.That(
                    collider.enabled,
                    Is.EqualTo(shouldBeEnabled),
                    context + ":" + colliderIndex);
            }
        }

        private static bool ContainsSpritePath(
            Transform root,
            string expectedPath)
        {
            SpriteRenderer[] renderers =
                root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Sprite sprite = renderers[index].sprite;
                if (sprite != null
                    && AssetDatabase.GetAssetPath(sprite) == expectedPath)
                {
                    return true;
                }
            }

            return false;
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
