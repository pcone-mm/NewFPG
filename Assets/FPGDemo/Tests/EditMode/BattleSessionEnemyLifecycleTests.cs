using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleSessionEnemyLifecycleTests
    {
        [Test]
        public void SpawnDefinitionCannotReuseInitialEnemyDefinitionId()
        {
            Assert.That(
                () => CombatLabHarness.CreateScenario(
                    enemySpawns: new[]
                    {
                        new EnemySpawnDefinition(
                            1,
                            new TickIndex(3),
                            77,
                            23,
                            new TickDuration(20))
                    }),
                Throws.ArgumentException);
        }

        [Test]
        public void HatchingDespawnsEggAndSpawnsIndependentButterflyWithoutCompletingBattle()
        {
            EnemySpawnDefinition butterfly = new EnemySpawnDefinition(
                2,
                new TickIndex(3),
                77,
                23,
                new TickDuration(20));
            using (BattleSession session = new BattleSessionFactory().Create(
                       CombatLabHarness.CreateScenario(enemySpawns: new[] { butterfly }),
                       new NullAttackResolutionPort()))
            {
                RuntimeId eggRuntimeId = session.EnemyRuntimeId;
                int lifecycleChangeCount = 0;
                EnemyLifecycleChange lifecycleChange = default(EnemyLifecycleChange);
                session.EnemyRuntimeChanged += change =>
                {
                    lifecycleChangeCount++;
                    lifecycleChange = change;
                };

                Assert.That(session.ApplyControl(new SessionControlCommand(
                    new ControlSequence(1),
                    SessionControlCommandType.Start)).IsSuccess, Is.True);
                CombatLabHarness.PumpTicks(session, 3);
                Assert.That(session.EnemyRuntimeId, Is.EqualTo(eggRuntimeId));

                CombatLabHarness.PumpOneTick(session);

                Assert.That(lifecycleChangeCount, Is.EqualTo(1));
                Assert.That(lifecycleChange.PreviousRuntimeId, Is.EqualTo(eggRuntimeId));
                Assert.That(lifecycleChange.CurrentRuntimeId, Is.EqualTo(session.EnemyRuntimeId));
                Assert.That(lifecycleChange.CurrentRuntimeId, Is.Not.EqualTo(eggRuntimeId));
                Assert.That(lifecycleChange.DefinitionId, Is.EqualTo(2));
                Assert.That(session.EnemyRuntimeCount, Is.EqualTo(2));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.None));

                FinalSnapshot snapshot = session.GetFinalSnapshot();
                Assert.That(snapshot.EnemyLife, Is.EqualTo(77));
                Assert.That(snapshot.EnemyBreak, Is.EqualTo(23));
                Assert.That(snapshot.EnemyMaxLife, Is.EqualTo(77));
                Assert.That(snapshot.EnemyMaxBreak, Is.EqualTo(23));
                Assert.That(snapshot.EnemyDefinitionId, Is.EqualTo(2));

                bool sawEggDeath = false;
                bool sawDespawn = false;
                bool sawButterflySpawn = false;
                for (int index = 0; index < session.Trace.Count; index++)
                {
                    CombatEvent combatEvent = session.Trace.GetOldest(index);
                    if (combatEvent.EventType == CombatEventType.Death
                        && combatEvent.TargetId == eggRuntimeId)
                    {
                        sawEggDeath = true;
                    }

                    if (combatEvent.EventType == CombatEventType.EnemyDespawned
                        && combatEvent.TargetId == eggRuntimeId)
                    {
                        sawDespawn = true;
                    }

                    if (combatEvent.EventType == CombatEventType.EnemySpawned
                        && combatEvent.TargetId == session.EnemyRuntimeId)
                    {
                        sawButterflySpawn = true;
                        Assert.That(combatEvent.SourceId, Is.EqualTo(eggRuntimeId));
                        Assert.That(combatEvent.ValueBefore, Is.EqualTo(2));
                        Assert.That(combatEvent.ValueAfter, Is.EqualTo(77));
                        Assert.That(combatEvent.PayloadHash, Is.EqualTo(23UL));
                    }

                    Assert.That(combatEvent.EventType, Is.Not.EqualTo(CombatEventType.BattleCompleted));
                }

                Assert.That(sawEggDeath, Is.True);
                Assert.That(sawDespawn, Is.True);
                Assert.That(sawButterflySpawn, Is.True);
            }
        }
    }
}
