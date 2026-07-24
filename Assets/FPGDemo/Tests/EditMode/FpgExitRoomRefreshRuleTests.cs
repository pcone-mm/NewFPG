using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgExitRoomRefreshRuleTests
    {
        private const string RoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";

        [Test]
        public void RouteSelectionIsDeterministicAndStableIdOrdered()
        {
            FpgExitRefreshContext context = CreateContext(roomVisitOrdinal: 7);
            string[] candidateRoomIds = { "room-c", "room-a", "room-b" };
            string[] exitIds = { "exit-z", "exit-a", "exit-m" };

            Assert.That(
                FpgExitRouteSelector.TrySelect(
                    context,
                    candidateRoomIds,
                    exitIds,
                    out FpgExitRouteDecision[] first,
                    out string error),
                Is.True,
                error);
            Assert.That(
                FpgExitRouteSelector.TrySelect(
                    context,
                    new[] { "room-b", "room-c", "room-a" },
                    new[] { "exit-m", "exit-z", "exit-a" },
                    out FpgExitRouteDecision[] reordered,
                    out error),
                Is.True,
                error);

            CollectionAssert.AreEqual(first, reordered);
            Assert.That(first[0].ExitId, Is.EqualTo("exit-a"));
            Assert.That(first[1].ExitId, Is.EqualTo("exit-m"));
            Assert.That(first[2].ExitId, Is.EqualTo("exit-z"));
            Assert.That(
                CollectDestinationIds(first),
                Is.EquivalentTo(candidateRoomIds),
                "A complete candidate cycle must include the current room as well.");
        }

        [Test]
        public void RouteSelectionDoesNotRepeatUntilEachPoolCycleIsExhausted()
        {
            string[] exitIds =
            {
                "exit-07",
                "exit-02",
                "exit-05",
                "exit-01",
                "exit-06",
                "exit-03",
                "exit-04"
            };

            Assert.That(
                FpgExitRouteSelector.TrySelect(
                    CreateContext(roomVisitOrdinal: 3),
                    new[] { "room-a", "room-b", "room-c" },
                    exitIds,
                    out FpgExitRouteDecision[] decisions,
                    out string error),
                Is.True,
                error);

            AssertDistinctDestinations(decisions, 0, 3);
            AssertDistinctDestinations(decisions, 3, 3);
            Assert.That(decisions, Has.Length.EqualTo(exitIds.Length));
        }

        [Test]
        public void VisitOrdinalParticipatesInTheRouteDecisionAndReroll()
        {
            string[] rooms = { "room-a", "room-b", "room-c", "room-d" };
            string[] exits = { "exit-main" };
            HashSet<string> observedDestinations = new HashSet<string>(StringComparer.Ordinal);

            for (int ordinal = 0; ordinal < 16; ordinal++)
            {
                Assert.That(
                    FpgExitRouteSelector.TrySelect(
                        CreateContext(ordinal),
                        rooms,
                        exits,
                        out FpgExitRouteDecision[] decisions,
                        out string error),
                    Is.True,
                    error);
                Assert.That(decisions[0].RoomVisitOrdinal, Is.EqualTo(ordinal));
                observedDestinations.Add(decisions[0].DestinationRoomId);
            }

            Assert.That(
                observedDestinations.Count,
                Is.GreaterThan(1),
                "Changing the room visit ordinal must drive a fresh deterministic draw.");
        }

        [Test]
        public void RouteSelectionRejectsMissingSourceAndDuplicateStableIds()
        {
            Assert.That(
                FpgExitRouteSelector.TrySelect(
                    CreateContext(roomVisitOrdinal: 0),
                    new[] { "room-a", "room-c" },
                    new[] { "exit-main" },
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("Source room"));

            Assert.That(
                FpgExitRouteSelector.TrySelect(
                    CreateContext(roomVisitOrdinal: 0),
                    new[] { "room-a", "room-b" },
                    new[] { "exit-main", "exit-main" },
                    out _,
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("duplicate exit"));
        }

        [Test]
        public void RefreshRuleResolvesFrozenOffersFromTheValidatedCatalog()
        {
            FpgRoomDefinition roomA = CreateRoomClone("room-a", "Room A");
            FpgRoomDefinition roomB = CreateRoomClone("room-b", "Room B");
            FpgRoomDefinition roomC = CreateRoomClone("room-c", "Room C");
            FpgRoomCatalog catalog = ScriptableObject.CreateInstance<FpgRoomCatalog>();
            FpgExitRoomRefreshRule rule =
                ScriptableObject.CreateInstance<FpgExitRoomRefreshRule>();
            try
            {
                SetRoomCatalogEntries(catalog, roomC, roomA, roomB);
                SetObjectReference(rule, "roomCatalog", catalog);

                Assert.That(catalog.TryValidate(out string error), Is.True, error);
                Assert.That(rule.TryValidate(out error), Is.True, error);
                Assert.That(
                    rule.TryCreateOffers(
                        CreateContext(roomVisitOrdinal: 9),
                        new[] { "exit-right", "exit-left", "exit-center" },
                        out FpgExitOffer[] offers,
                        out error),
                    Is.True,
                    error);

                Assert.That(offers, Has.Length.EqualTo(3));
                Assert.That(offers[0].ExitId, Is.EqualTo("exit-center"));
                Assert.That(offers[1].ExitId, Is.EqualTo("exit-left"));
                Assert.That(offers[2].ExitId, Is.EqualTo("exit-right"));
                Assert.That(
                    CollectDestinationIds(offers),
                    Is.EquivalentTo(new[] { "room-a", "room-b", "room-c" }));
                for (int index = 0; index < offers.Length; index++)
                {
                    FpgExitOffer offer = offers[index];
                    Assert.That(offer.IsValid, Is.True);
                    Assert.That(offer.SourceRoomId, Is.EqualTo("room-b"));
                    Assert.That(offer.DestinationRoomId, Is.EqualTo(offer.DestinationRoom.RoomId));
                    Assert.That(
                        offer.DestinationDisplayName,
                        Is.EqualTo(offer.DestinationRoom.DisplayName));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rule);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(roomC);
                UnityEngine.Object.DestroyImmediate(roomB);
                UnityEngine.Object.DestroyImmediate(roomA);
            }
        }

        [Test]
        public void RoomCatalogRejectsMissingExitAndDuplicateRoomIds()
        {
            FpgRoomDefinition room = CreateRoomClone("room-a", "Room A");
            FpgRoomCatalog catalog = ScriptableObject.CreateInstance<FpgRoomCatalog>();
            try
            {
                SerializedObject serializedRoom = new SerializedObject(room);
                serializedRoom.FindProperty("exitSlots").arraySize = 0;
                serializedRoom.ApplyModifiedPropertiesWithoutUndo();
                SetRoomCatalogEntries(catalog, room);

                Assert.That(catalog.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("exit slot"));

                EnsureMainExit(room);
                SetRoomCatalogEntries(catalog, room, room);
                Assert.That(catalog.TryValidate(out error), Is.False);
                Assert.That(error, Does.Contain("duplicate room ID"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(room);
            }
        }

        private static FpgExitRefreshContext CreateContext(int roomVisitOrdinal)
        {
            return new FpgExitRefreshContext(
                new FpgEncounterRunContext(
                    runSeed: 0x123456789ABCDEF0UL,
                    regionId: "test-region",
                    depth: 0,
                    difficultyMultiplierBasisPoints:
                        FpgEncounterRunContext.BasisPointsOne,
                    roomVisitOrdinal: roomVisitOrdinal),
                sourceRoomId: "room-b");
        }

        private static FpgRoomDefinition CreateRoomClone(
            string roomId,
            string displayName)
        {
            FpgRoomDefinition source =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(RoomPath);
            Assert.That(source, Is.Not.Null, $"Required room asset is missing: {RoomPath}");
            FpgRoomDefinition clone = UnityEngine.Object.Instantiate(source);
            SerializedObject serialized = new SerializedObject(clone);
            serialized.FindProperty("roomId").stringValue = roomId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EnsureMainExit(clone);
            Assert.That(
                clone.TryValidate(out FpgRoomValidationResult validation),
                Is.True,
                validation.FirstError == null ? string.Empty : validation.FirstError.Message);
            return clone;
        }

        private static void EnsureMainExit(FpgRoomDefinition room)
        {
            SerializedObject serialized = new SerializedObject(room);
            SerializedProperty exits = serialized.FindProperty("exitSlots");
            exits.arraySize = 1;
            SerializedProperty exit = exits.GetArrayElementAtIndex(0);
            exit.FindPropertyRelative("markerId").stringValue = "exit-main";
            exit.FindPropertyRelative("displayName").stringValue = "Main Exit";
            exit.FindPropertyRelative("localPosition").vector3Value =
                new Vector3(0f, 1.5f, 20.5f);
            exit.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRoomCatalogEntries(
            FpgRoomCatalog catalog,
            params FpgRoomDefinition[] rooms)
        {
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("rooms");
            entries.arraySize = rooms.Length;
            for (int index = 0; index < rooms.Length; index++)
            {
                entries.GetArrayElementAtIndex(index).objectReferenceValue = rooms[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(owner);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HashSet<string> CollectDestinationIds(
            IReadOnlyList<FpgExitRouteDecision> decisions)
        {
            HashSet<string> destinationIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < decisions.Count; index++)
            {
                destinationIds.Add(decisions[index].DestinationRoomId);
            }

            return destinationIds;
        }

        private static HashSet<string> CollectDestinationIds(
            IReadOnlyList<FpgExitOffer> offers)
        {
            HashSet<string> destinationIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < offers.Count; index++)
            {
                destinationIds.Add(offers[index].DestinationRoomId);
            }

            return destinationIds;
        }

        private static void AssertDistinctDestinations(
            IReadOnlyList<FpgExitRouteDecision> decisions,
            int start,
            int count)
        {
            HashSet<string> destinationIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = start; index < start + count; index++)
            {
                Assert.That(
                    destinationIds.Add(decisions[index].DestinationRoomId),
                    Is.True,
                    $"Destination repeated before pool cycle was exhausted at index {index}.");
            }
        }
    }
}
