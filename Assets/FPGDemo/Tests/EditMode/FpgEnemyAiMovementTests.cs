using FPG.Demo.Core;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgEnemyAiMovementTests
    {
        [Test]
        public void StationaryExecutorNeverChangesPositionOrCreatesGoal()
        {
            FpgEnemyStationaryMovementExecutor executor =
                new FpgEnemyStationaryMovementExecutor();
            Vector3 start = new Vector3(2f, 3f, 4f);
            FpgEnemyMovementRequest request =
                new FpgEnemyMovementRequest(true, true, 5f);

            FpgEnemyMovementSnapshot snapshot = executor.Advance(
                new TickIndex(0),
                start,
                in request);

            Assert.That(snapshot.State, Is.EqualTo(FpgEnemyMovementExecutionState.Idle));
            Assert.That(snapshot.Position, Is.EqualTo(start));
            Assert.That(snapshot.Delta, Is.EqualTo(Vector3.zero));
            Assert.That(snapshot.HasGoal, Is.False);
        }

        [Test]
        public void ChaseBehaviorRequiresPositiveMovementSpeed()
        {
            FpgEnemyBehaviorDefinition behavior =
                ScriptableObject.CreateInstance<FpgEnemyBehaviorDefinition>();
            FpgEnemyPositionQueryDefinition positionQuery =
                ScriptableObject.CreateInstance<FpgEnemyPositionQueryDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(behavior);
                serialized.FindProperty("mode").enumValueIndex =
                    (int)FpgEnemyBehaviorMode.Chase;
                serialized.FindProperty("positionQuery").objectReferenceValue =
                    positionQuery;
                serialized.FindProperty("moveSpeed").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(behavior.TryValidate(out _), Is.False);

                serialized.FindProperty("moveSpeed").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(behavior.TryValidate(out string error), Is.True, error);
            }
            finally
            {
                Object.DestroyImmediate(behavior);
                Object.DestroyImmediate(positionQuery);
            }
        }

        [Test]
        public void LateralChaseMovesOnGroundPlaneAndStopsAtConfiguredDistance()
        {
            FpgEnemyBehaviorDefinition behavior =
                ScriptableObject.CreateInstance<FpgEnemyBehaviorDefinition>();
            GameObject target = new GameObject("LateralChaseTarget");
            try
            {
                SerializedObject serialized = new SerializedObject(behavior);
                serialized.FindProperty("mode").enumValueIndex =
                    (int)FpgEnemyBehaviorMode.LateralChase;
                serialized.FindProperty("lateralAttackDistance").floatValue = 1.2f;
                serialized.FindProperty("lateralStopDistance").floatValue = 0.35f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                target.transform.position = new Vector3(0f, -4f, 10f);
                FpgEnemyLateralChaseMovementExecutor executor =
                    new FpgEnemyLateralChaseMovementExecutor(behavior);
                executor.ConfigureContext(
                    new FpgEnemyMovementContext(
                        null,
                        target.transform,
                        null,
                        null,
                        null,
                        0,
                        -1));

                Vector3 current = new Vector3(0f, 2f, 0f);
                FpgEnemyMovementRequest request =
                    new FpgEnemyMovementRequest(true, true, 60f);
                FpgEnemyMovementSnapshot snapshot = default;
                bool stopped = false;
                for (int tick = 0; tick < 16; tick++)
                {
                    snapshot = executor.Advance(
                        new TickIndex(tick),
                        current,
                        in request);
                    current = snapshot.Position;
                    Assert.That(snapshot.Position.y, Is.EqualTo(2f));
                    if (snapshot.State == FpgEnemyMovementExecutionState.Idle)
                    {
                        stopped = true;
                        break;
                    }
                }

                Assert.That(stopped, Is.True);
                Assert.That(snapshot.State, Is.EqualTo(FpgEnemyMovementExecutionState.Idle));
                Assert.That(
                    Vector2.Distance(
                        new Vector2(current.x, current.z),
                        new Vector2(target.transform.position.x, target.transform.position.z)),
                    Is.LessThanOrEqualTo(0.351f));
                Assert.That(executor.CanStartAttack(current), Is.True);
                Assert.That(
                    executor.CanStartAttack(new Vector3(0f, 2f, 8.79f)),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(behavior);
            }
        }
    }
}
