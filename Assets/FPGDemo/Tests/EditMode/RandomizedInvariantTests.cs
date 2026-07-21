using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class RandomizedInvariantTests
    {
        private const int CorpusCount = 1000;
        private const int OperationsPerCorpus = 64;

        [Test]
        public void RandomizedProjectileBudgetOperationsPreserveConservation()
        {
            for (int corpus = 0; corpus < CorpusCount; corpus++)
            {
                uint state = unchecked((uint)(0x9E3779B9U + corpus * 747796405U));
                ProjectileBudget budget = new ProjectileBudget(12, 32);
                ReservationToken[] tokens = new ReservationToken[32];
                int[] remainingUnits = new int[32];
                bool[] active = new bool[32];
                int tokenCount = 0;

                for (int operation = 0; operation < OperationsPerCorpus; operation++)
                {
                    uint sample = Next(ref state);
                    int action = (int)(sample % 4U);

                    if (action == 0 && tokenCount < tokens.Length)
                    {
                        int units = 1 + (int)((sample >> 8) % 4U);
                        DomainResult reserve = budget.TryReserve(units, out ReservationToken token);
                        if (reserve.IsSuccess)
                        {
                            tokens[tokenCount] = token;
                            remainingUnits[tokenCount] = units;
                            active[tokenCount] = false;
                            tokenCount++;
                        }
                    }
                    else if (tokenCount > 0)
                    {
                        int index = (int)((sample >> 16) % (uint)tokenCount);
                        if (!tokens[index].IsValid)
                        {
                            AssertBudgetInvariant(budget, corpus, operation);
                            continue;
                        }

                        if (!active[index] && action == 1)
                        {
                            if (budget.Activate(tokens[index]).IsSuccess)
                            {
                                active[index] = true;
                            }
                        }
                        else if (!active[index] && action == 2)
                        {
                            if (budget.ReleaseReservation(tokens[index]).IsSuccess)
                            {
                                tokens[index] = default(ReservationToken);
                                remainingUnits[index] = 0;
                            }
                        }
                        else if (active[index] && action == 3)
                        {
                            int release = 1 + (int)((sample >> 24) % (uint)remainingUnits[index]);
                            Assert.That(
                                budget.ReleaseActive(tokens[index], release).IsSuccess,
                                Is.True,
                                FailureMessage(corpus, operation, state));
                            remainingUnits[index] -= release;
                            if (remainingUnits[index] == 0)
                            {
                                tokens[index] = default(ReservationToken);
                            }
                        }
                    }

                    AssertBudgetInvariant(budget, corpus, operation);
                }
            }
        }

        [Test]
        public void RandomizedProjectileTerminalTransitionsNeverResurrect()
        {
            for (int corpus = 0; corpus < CorpusCount; corpus++)
            {
                uint state = unchecked((uint)(0xA341316CU + corpus * 2246822519U));
                ProjectileRuntime projectile = CreateProjectile(corpus + 1);
                bool terminalObserved = false;

                for (int operation = 0; operation < 16; operation++)
                {
                    ProjectileState before = projectile.State;
                    switch (Next(ref state) % 4U)
                    {
                        case 0:
                            projectile.StartTravelling();
                            break;
                        case 1:
                            projectile.TryHit();
                            break;
                        case 2:
                            projectile.TryExpire(new TickIndex(99));
                            break;
                        default:
                            projectile.TryCancel(new TickIndex(99));
                            break;
                    }

                    if (terminalObserved)
                    {
                        Assert.That(
                            projectile.State,
                            Is.EqualTo(before),
                            FailureMessage(corpus, operation, state));
                    }

                    terminalObserved |= projectile.IsTerminal;
                    Assert.That(projectile.HitPoints, Is.GreaterThanOrEqualTo(0));
                    Assert.That(projectile.HitPoints, Is.LessThanOrEqualTo(projectile.Definition.MaxHitPoints));
                }
            }
        }

        [Test]
        public void RandomizedThreatSequencesNeverExceedBudgetOrReleaseTwice()
        {
            for (int corpus = 0; corpus < CorpusCount; corpus++)
            {
                uint state = unchecked((uint)(0xC8013EA4U + corpus * 3266489917U));
                ProjectileBudget budget = new ProjectileBudget(6, 32);
                SessionIdAllocator ids = new SessionIdAllocator();
                ThreatRuntime[] threats = new ThreatRuntime[6];

                for (int index = 0; index < threats.Length; index++)
                {
                    int payloadCount = 1 + (int)(Next(ref state) % 3U);
                    threats[index] = new ThreatRuntime(
                        CombatLabHarness.CreateThreatDefinition(payloadCount: payloadCount));
                }

                for (int operation = 0; operation < OperationsPerCorpus; operation++)
                {
                    int index = (int)(Next(ref state) % (uint)threats.Length);
                    ThreatRuntime threat = threats[index];
                    TickIndex tick = new TickIndex(operation);

                    switch (Next(ref state) % 5U)
                    {
                        case 0:
                            threat.TryStart(tick, EnemyControlState.Active, budget, ids);
                            break;
                        case 1:
                            threat.AdvanceBeforeRelease(tick);
                            break;
                        case 2:
                            threat.TryCommitRelease(tick, budget, out ThreatRelease ignored);
                            break;
                        case 3:
                            if (threat.State == ThreatState.ReleaseCommitted)
                            {
                                threat.ConfirmPayloadsCreated(tick);
                            }
                            break;
                        default:
                            threat.TryCancelBeforeRelease(budget);
                            break;
                    }

                    AssertBudgetInvariant(budget, corpus, operation);
                    for (int threatIndex = 0; threatIndex < threats.Length; threatIndex++)
                    {
                        ThreatRuntime candidate = threats[threatIndex];
                        if (candidate.HasReleased)
                        {
                            Assert.That(candidate.AttackId.IsValid, Is.True);
                            Assert.That(candidate.ReservationToken.IsValid, Is.True);
                        }
                    }
                }

                budget.CancelAll();
                Assert.That(budget.ReservedUnits, Is.Zero);
                Assert.That(budget.ActiveUnits, Is.Zero);
            }
        }

        private static ProjectileRuntime CreateProjectile(int offset)
        {
            ProjectileDefinition definition = new ProjectileDefinition(
                1,
                new TickDuration(2),
                new TickDuration(4),
                new DamageSpec(1, 0),
                3,
                true,
                1);
            return new ProjectileRuntime(
                new ProjectileId(offset),
                new RuntimeId(offset + 1000),
                new AttackId(offset),
                new RuntimeId(2),
                Team.Enemy,
                definition,
                new TickIndex(0),
                default(ReservationToken));
        }

        private static uint Next(ref uint state)
        {
            state = unchecked(state * 1664525U + 1013904223U);
            return state;
        }

        private static void AssertBudgetInvariant(
            ProjectileBudget budget,
            int corpus,
            int operation)
        {
            Assert.That(
                budget.ReservedUnits,
                Is.GreaterThanOrEqualTo(0),
                FailureMessage(corpus, operation, 0));
            Assert.That(
                budget.ActiveUnits,
                Is.GreaterThanOrEqualTo(0),
                FailureMessage(corpus, operation, 0));
            Assert.That(
                budget.ReservedUnits + budget.ActiveUnits,
                Is.LessThanOrEqualTo(budget.Capacity),
                FailureMessage(corpus, operation, 0));
        }

        private static string FailureMessage(int corpus, int operation, uint state)
        {
            return "Corpus=" + corpus + ", Operation=" + operation + ", State=" + state;
        }
    }
}
