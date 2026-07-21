using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class WarningPresentationAnchorTests
    {
        [Test]
        public void CatalogRejectsKeyThreeWithoutEnemyWeakpointAnchorKind()
        {
            GameObject prefabObject = CreateWarningPrefab();
            BattlePresentationCatalog catalog = CreateCatalog(
                prefabObject.GetComponent<WarningView>(),
                new WarningEntrySpec(
                    BattlePresentationCatalog.WeakpointWarningPresentationKey,
                    WarningAnchorKind.PlayerGround,
                    1));
            try
            {
                Assert.That(
                    catalog.TryValidateWarningCoverage(CreateWeakpointScenario(1), out string error),
                    Is.False);
                Assert.That(error, Does.Contain("must use the EnemyWeakpoint anchor kind"));
            }
            finally
            {
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void PoolUsesCatalogAnchorKindForGroundAndWeakpointWarnings()
        {
            GameObject root = new GameObject("WarningAnchorPoolRoot");
            GameObject prefabObject = CreateWarningPrefab();
            GameObject cameraObject = new GameObject("WarningAnchorCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            BattlePresentationCatalog catalog = CreateCatalog(
                prefabObject.GetComponent<WarningView>(),
                new WarningEntrySpec(1, WarningAnchorKind.PlayerGround, 1),
                new WarningEntrySpec(
                    BattlePresentationCatalog.WeakpointWarningPresentationKey,
                    WarningAnchorKind.EnemyWeakpoint,
                    2));
            WarningViewPool pool = new WarningViewPool();
            try
            {
                camera.transform.position = new Vector3(-1f, 4f, -5f);
                Assert.That(
                    pool.TryPrepare(
                        CreateWeakpointScenario(2),
                        catalog,
                        root.transform,
                        camera,
                        out string error),
                    Is.True,
                    error);

                ThreatSnapshot[] snapshots =
                {
                    CreateSnapshot(101, 1),
                    CreateSnapshot(
                        102,
                        BattlePresentationCatalog.WeakpointWarningPresentationKey)
                };
                Vector3 playerGroundPosition = new Vector3(-2f, 0f, 1f);
                Vector3 enemyWeakpointPosition = new Vector3(4f, 2.4f, 7f);

                pool.Reconcile(
                    snapshots,
                    snapshots.Length,
                    playerGroundPosition,
                    enemyWeakpointPosition);

                Assert.That(pool.TryGet(snapshots[0].RuntimeId, out WarningView groundView), Is.True);
                Assert.That(pool.TryGet(snapshots[1].RuntimeId, out WarningView weakpointView), Is.True);
                Assert.That(groundView.AnchorKind, Is.EqualTo(WarningAnchorKind.PlayerGround));
                Assert.That(weakpointView.AnchorKind, Is.EqualTo(WarningAnchorKind.EnemyWeakpoint));
                Assert.That(
                    Vector3.Distance(
                        groundView.transform.position,
                        playerGroundPosition + Vector3.up * 0.025f),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Vector3.Distance(
                        weakpointView.transform.position,
                        enemyWeakpointPosition + Vector3.up * 0.025f),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        groundView.transform.rotation,
                        Quaternion.Euler(90f, 0f, 0f)),
                    Is.LessThan(0.001f));
                Assert.That(weakpointView.BillboardCamera, Is.SameAs(camera));
                Vector3 weakpointToCamera =
                    (camera.transform.position - weakpointView.transform.position).normalized;
                Assert.That(
                    Vector3.Dot(weakpointView.transform.forward, weakpointToCamera),
                    Is.GreaterThan(0.999f));
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PoolRejectsWeakpointWarningWithoutAnExplicitBillboardCamera()
        {
            GameObject root = new GameObject("WarningAnchorCameraContractRoot");
            GameObject prefabObject = CreateWarningPrefab();
            BattlePresentationCatalog catalog = CreateCatalog(
                prefabObject.GetComponent<WarningView>(),
                new WarningEntrySpec(
                    BattlePresentationCatalog.WeakpointWarningPresentationKey,
                    WarningAnchorKind.EnemyWeakpoint,
                    1));
            WarningViewPool pool = new WarningViewPool();
            try
            {
                Assert.That(
                    pool.TryPrepare(CreateWeakpointScenario(1), catalog, root.transform, out string error),
                    Is.False);
                Assert.That(error, Does.Contain("requires an explicit billboard camera"));
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateWarningPrefab()
        {
            GameObject prefabObject = new GameObject("WarningViewPrefab", typeof(SpriteRenderer));
            prefabObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            prefabObject.AddComponent<WarningView>();
            return prefabObject;
        }

        private static BattlePresentationCatalog CreateCatalog(
            WarningView warningPrefab,
            params WarningEntrySpec[] entries)
        {
            BattlePresentationCatalog catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty warningEntries = serialized.FindProperty("warningEntries");
            warningEntries.arraySize = entries.Length;
            for (int index = 0; index < entries.Length; index++)
            {
                WarningEntrySpec spec = entries[index];
                SerializedProperty entry = warningEntries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("presentationKey").intValue = spec.PresentationKey;
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = warningPrefab;
                entry.FindPropertyRelative("prewarmCapacity").intValue = spec.PrewarmCapacity;
                entry.FindPropertyRelative("anchorKind").enumValueIndex = (int)spec.AnchorKind;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static ScenarioDefinition CreateWeakpointScenario(int threatCapacity)
        {
            ThreatPayloadDefinition payload = ThreatPayloadDefinition.TimedImpact(
                new DamageSpec(10, 0),
                ThreatTargetPolicy.PlayerCombatant,
                TickDuration.Zero,
                BattlePresentationCatalog.WeakpointWarningPresentationKey);
            ThreatScheduleEntry scheduleEntry = new ThreatScheduleEntry(
                1,
                new TickIndex(10),
                901,
                new TickDuration(4),
                new TickDuration(4),
                new TickDuration(1),
                payload,
                ThreatRetryPolicy.HoldPendingNextTick);
            return CombatLabHarness.CreateScenario(
                threatCapacity: threatCapacity,
                threatSchedule: new[] { scheduleEntry });
        }

        private static ThreatSnapshot CreateSnapshot(long runtimeId, int presentationKey)
        {
            return new ThreatSnapshot(
                new RuntimeId(runtimeId),
                901,
                ThreatState.Telegraph,
                new AttackId(1),
                new TickIndex(4),
                false,
                false,
                ThreatPayloadKind.TimedImpact,
                presentationKey,
                ThreatTargetPolicy.PlayerCombatant);
        }

        private readonly struct WarningEntrySpec
        {
            public WarningEntrySpec(
                int presentationKey,
                WarningAnchorKind anchorKind,
                int prewarmCapacity)
            {
                PresentationKey = presentationKey;
                AnchorKind = anchorKind;
                PrewarmCapacity = prewarmCapacity;
            }

            public int PresentationKey { get; }
            public WarningAnchorKind AnchorKind { get; }
            public int PrewarmCapacity { get; }
        }
    }
}
