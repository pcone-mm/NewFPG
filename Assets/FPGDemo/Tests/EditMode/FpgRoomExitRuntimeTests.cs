using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgRoomExitRuntimeTests
    {
        private readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            owned.Clear();
        }

        [Test]
        public void RevealMovesHiddenExitToAvailableAndShowsLabel()
        {
            FpgExitOffer offer = CreateOffer(
                "exit-main",
                CreateRoom("room-a", "Destination A"));
            GameObject owner = Own(new GameObject("ExitRuntime"));
            BoxCollider collider = owner.AddComponent<BoxCollider>();
            Text label = CreateLabel(owner.transform);
            FpgRoomExitRuntime runtime = owner.AddComponent<FpgRoomExitRuntime>();
            runtime.BindComponents(new Collider[] { collider }, Array.Empty<Behaviour>());
            runtime.BindDestinationLabel(label);

            Assert.That(
                runtime.TryConfigure(
                    offer.ExitId,
                    new Pose(Vector3.zero, Quaternion.identity),
                    null,
                    out string error),
                Is.True,
                error);
            Assert.That(runtime.State, Is.EqualTo(FpgRoomExitRuntimeState.Hidden));
            Assert.That(runtime.Offer, Is.Null);
            Assert.That(collider.enabled, Is.False);
            Assert.That(label.enabled, Is.False);

            Assert.That(runtime.TryReveal(offer, out error), Is.True, error);
            Assert.That(runtime.State, Is.EqualTo(FpgRoomExitRuntimeState.Available));
            Assert.That(runtime.IsLocked, Is.False);
            Assert.That(runtime.Offer, Is.SameAs(offer));
            Assert.That(collider.enabled, Is.True);
            Assert.That(label.enabled, Is.True);
            Assert.That(label.text, Is.EqualTo("\u524d\u5f80\uff1aDestination A"));
        }

        [Test]
        public void AvailableExitKeepsFirstOfferAndCanOnlyBeConsumedOnce()
        {
            FpgExitOffer first = CreateOffer(
                "exit-main",
                CreateRoom("room-a", "Destination A"));
            FpgRoomExitRuntime runtime =
                CreateAvailableRuntime(first, out BoxCollider collider);
            FpgExitOffer competing = CreateOffer(
                "exit-main",
                CreateRoom("room-b", "Destination B"));

            Assert.That(
                runtime.TryReveal(competing, out string error),
                Is.False);
            Assert.That(error, Does.Contain("only be bound once"));
            Assert.That(runtime.Offer, Is.SameAs(first));

            int selectionCount = 0;
            runtime.Selected += _ => selectionCount++;
            Assert.That(runtime.TrySelect(), Is.True);
            Assert.That(runtime.State, Is.EqualTo(FpgRoomExitRuntimeState.Consumed));
            Assert.That(runtime.IsLocked, Is.True);
            Assert.That(runtime.Offer, Is.SameAs(first));
            Assert.That(collider.enabled, Is.False);
            runtime.SetLocked(false);
            Assert.That(
                runtime.State,
                Is.EqualTo(FpgRoomExitRuntimeState.Consumed));
            Assert.That(collider.enabled, Is.False);
            Assert.That(runtime.TrySelect(), Is.False);
            Assert.That(selectionCount, Is.EqualTo(1));
        }

        [Test]
        public void AttackRegistryResolvesEnvironmentOfferAndClearIsIdempotent()
        {
            FpgExitOffer offer = CreateOffer(
                "exit-main",
                CreateRoom("room-a", "Destination A"));
            FpgRoomExitRuntime runtime =
                CreateAvailableRuntime(offer, out BoxCollider collider);
            HitboxRegistry hitboxes =
                Own(new GameObject("HitboxRegistry")).AddComponent<HitboxRegistry>();
            FpgExitAttackRegistry registry = new FpgExitAttackRegistry();
            int nextGeometryValue = FpgExitAttackRegistry.GeometryIdStart;

            Assert.That(
                registry.TryRegisterRuntime(
                    runtime,
                    hitboxes,
                    ref nextGeometryValue,
                    out string error),
                Is.True,
                error);
            GeometryId geometryId =
                new GeometryId(FpgExitAttackRegistry.GeometryIdStart);
            Assert.That(nextGeometryValue, Is.EqualTo(geometryId.Value + 1));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(
                registry.TryGetAvailableOffer(geometryId, out FpgExitOffer resolvedOffer),
                Is.True);
            Assert.That(resolvedOffer, Is.SameAs(offer));
            Assert.That(
                registry.TryGetRuntime(geometryId, out FpgRoomExitRuntime resolvedRuntime),
                Is.True);
            Assert.That(resolvedRuntime, Is.SameAs(runtime));

            QueryCandidate[] blockedCandidates =
            {
                CreateBlockerCandidate(
                    AttackQueryStage.Pellet,
                    0,
                    new GeometryId(94000),
                    100,
                    0),
                CreateBlockerCandidate(
                    AttackQueryStage.Pellet,
                    0,
                    geometryId,
                    200,
                    1)
            };
            Assert.That(
                registry.TryFindFirstVisibleExit(
                    blockedCandidates,
                    blockedCandidates.Length,
                    out _),
                Is.False);

            QueryCandidate[] visibleCandidates =
            {
                CreateBlockerCandidate(
                    AttackQueryStage.Direct,
                    -1,
                    geometryId,
                    100,
                    0),
                CreateBlockerCandidate(
                    AttackQueryStage.Direct,
                    -1,
                    new GeometryId(94000),
                    200,
                    1)
            };
            Assert.That(
                registry.TryFindFirstVisibleExit(
                    visibleCandidates,
                    visibleCandidates.Length,
                    out GeometryId visibleExit),
                Is.True);
            Assert.That(visibleExit, Is.EqualTo(geometryId));

            Assert.That(
                hitboxes.TryResolve(geometryId, out RegisteredHitbox binding),
                Is.True);
            Assert.That(binding.Collider, Is.SameAs(collider));
            Assert.That(binding.TargetKind, Is.EqualTo(QueryTargetKind.EnvironmentBlocker));
            Assert.That(binding.RuntimeId.IsValid, Is.False);
            Assert.That(binding.Team, Is.EqualTo(Team.Neutral));

            Assert.That(runtime.TrySelect(), Is.True);
            Assert.That(registry.TryGetAvailableOffer(geometryId, out _), Is.False);
            registry.Clear();
            Assert.That(registry.Count, Is.Zero);
            Assert.That(hitboxes.Count, Is.Zero);
            Assert.That(hitboxes.TryResolve(geometryId, out _), Is.False);
            Assert.That(registry.TryGetRuntime(geometryId, out _), Is.False);
            Assert.DoesNotThrow(registry.Clear);
        }

        [Test]
        public void PreparedRuntimeCleanupDestroysRoomOwnedExits()
        {
            GameObject directorObject = Own(new GameObject("Director"));
            FpgRoomEncounterDirector director =
                directorObject.AddComponent<FpgRoomEncounterDirector>();
            FpgRoomExitRuntime first =
                new GameObject("OldExitA").AddComponent<FpgRoomExitRuntime>();
            FpgRoomExitRuntime second =
                new GameObject("OldExitB").AddComponent<FpgRoomExitRuntime>();
            first.transform.SetParent(directorObject.transform, false);
            second.transform.SetParent(directorObject.transform, false);

            FieldInfo ownedField = typeof(FpgRoomEncounterDirector).GetField(
                "ownedExitRuntimes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo activeField = typeof(FpgRoomEncounterDirector).GetField(
                "activeExits",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo clearMethod = typeof(FpgRoomEncounterDirector).GetMethod(
                "ClearPreparedRuntime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ownedField, Is.Not.Null);
            Assert.That(activeField, Is.Not.Null);
            Assert.That(clearMethod, Is.Not.Null);

            List<FpgRoomExitRuntime> ownedRuntimes =
                (List<FpgRoomExitRuntime>)ownedField.GetValue(director);
            ownedRuntimes.Add(first);
            ownedRuntimes.Add(second);
            activeField.SetValue(
                director,
                new[] { first, second });

            clearMethod.Invoke(director, new object[] { true });

            Assert.That(first == null, Is.True);
            Assert.That(second == null, Is.True);
            Assert.That(ownedRuntimes, Is.Empty);
            Assert.That(
                (FpgRoomExitRuntime[])activeField.GetValue(director),
                Is.Empty);
        }

        [TestCase(FpgExitAttackRegistry.GeometryIdStart - 1)]
        [TestCase(FpgExitAttackRegistry.GeometryIdEndExclusive)]
        public void AttackRegistryRejectsGeometryOutsideReservedRange(
            int nextGeometryValue)
        {
            FpgRoomExitRuntime runtime = CreateAvailableRuntime(
                CreateOffer(
                    "exit-main",
                    CreateRoom("room-a", "Destination A")),
                out _);
            HitboxRegistry hitboxes =
                Own(new GameObject("HitboxRegistry")).AddComponent<HitboxRegistry>();
            FpgExitAttackRegistry registry = new FpgExitAttackRegistry();
            int initialValue = nextGeometryValue;

            Assert.That(
                registry.TryRegisterRuntime(
                    runtime,
                    hitboxes,
                    ref nextGeometryValue,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("capacity"));
            Assert.That(nextGeometryValue, Is.EqualTo(initialValue));
            Assert.That(registry.Count, Is.Zero);
            Assert.That(hitboxes.Count, Is.Zero);
        }

        private Text CreateLabel(Transform parent)
        {
            GameObject labelObject = Own(new GameObject(
                "DestinationLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)));
            labelObject.transform.SetParent(parent, false);
            return labelObject.GetComponent<Text>();
        }

        private FpgRoomExitRuntime CreateAvailableRuntime(
            FpgExitOffer offer,
            out BoxCollider collider)
        {
            GameObject owner = Own(new GameObject("ExitRuntime"));
            collider = owner.AddComponent<BoxCollider>();
            FpgRoomExitRuntime runtime = owner.AddComponent<FpgRoomExitRuntime>();
            runtime.BindComponents(new Collider[] { collider }, Array.Empty<Behaviour>());
            Assert.That(
                runtime.TryConfigure(
                    offer.ExitId,
                    new Pose(Vector3.zero, Quaternion.identity),
                    null,
                    out string error),
                Is.True,
                error);
            Assert.That(runtime.TryReveal(offer, out error), Is.True, error);
            return runtime;
        }

        private static FpgExitOffer CreateOffer(
            string exitId,
            FpgRoomDefinition destination)
        {
            return new FpgExitOffer(
                new FpgExitRouteDecision(
                    "source-room",
                    exitId,
                    destination.RoomId,
                    roomVisitOrdinal: 4),
                destination);
        }

        private static QueryCandidate CreateBlockerCandidate(
            AttackQueryStage stage,
            int sampleIndex,
            GeometryId geometryId,
            int distanceKey,
            int queryOrdinal)
        {
            return new QueryCandidate(
                stage,
                sampleIndex,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                geometryId,
                distanceKey,
                SpatialVectorKey.Zero,
                queryOrdinal);
        }

        private FpgRoomDefinition CreateRoom(string roomId, string displayName)
        {
            FpgRoomDefinition room =
                Own(ScriptableObject.CreateInstance<FpgRoomDefinition>());
            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("roomId").stringValue = roomId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }

        private T Own<T>(T value)
            where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }
    }
}
