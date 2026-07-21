using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class ProjectileThreatTests
    {
        [Test]
        public void ThreatReservesItsWholePayloadAtomically()
        {
            ThreatDefinition definition = CombatLabHarness.CreateThreatDefinition(
                payloadCount: 3,
                projectileBudgetUnits: 2);
            ThreatRuntime threat = new ThreatRuntime(definition);
            ProjectileBudget budget = new ProjectileBudget(5);
            SessionIdAllocator ids = new SessionIdAllocator();

            DomainResult result = threat.TryStart(
                new TickIndex(0),
                EnemyControlState.Active,
                budget,
                ids);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.BudgetExceeded));
            Assert.That(threat.State, Is.EqualTo(ThreatState.Scheduled));
            Assert.That(threat.AttackId.IsValid, Is.False);
            Assert.That(budget.ReservedUnits, Is.Zero);
            Assert.That(budget.ActiveUnits, Is.Zero);

            ThreatRuntime subsequent = new ThreatRuntime(
                CombatLabHarness.CreateThreatDefinition(payloadCount: 1));
            Assert.That(subsequent.TryStart(
                new TickIndex(0),
                EnemyControlState.Active,
                new ProjectileBudget(1),
                ids).IsSuccess, Is.True);
            Assert.That(subsequent.AttackId.Value, Is.EqualTo(1));
        }

        [Test]
        public void CancelBeforeReleaseReturnsReservationAndIsTerminal()
        {
            ThreatRuntime threat = new ThreatRuntime(
                CombatLabHarness.CreateThreatDefinition(payloadCount: 2));
            ProjectileBudget budget = new ProjectileBudget(2);
            SessionIdAllocator ids = new SessionIdAllocator();

            Assert.That(threat.TryStart(
                new TickIndex(0),
                EnemyControlState.Active,
                budget,
                ids).IsSuccess, Is.True);
            Assert.That(budget.ReservedUnits, Is.EqualTo(2));

            Assert.That(threat.TryCancelBeforeRelease(budget).IsSuccess, Is.True);
            Assert.That(threat.State, Is.EqualTo(ThreatState.Canceled));
            Assert.That(budget.ReservedUnits, Is.Zero);
            Assert.That(budget.ActiveUnits, Is.Zero);

            DomainResult repeated = threat.TryCancelBeforeRelease(budget);
            Assert.That(repeated.IsSuccess, Is.False);
            Assert.That(repeated.RejectReason, Is.EqualTo(RejectReason.AlreadyTerminal));
        }

        [Test]
        public void ReleaseConvertsReservedBudgetToActiveAndCannotBeCanceled()
        {
            ThreatRuntime threat = new ThreatRuntime(
                CombatLabHarness.CreateThreatDefinition(payloadCount: 2));
            ProjectileBudget budget = new ProjectileBudget(2);
            SessionIdAllocator ids = new SessionIdAllocator();

            Assert.That(threat.TryStart(
                new TickIndex(0),
                EnemyControlState.Active,
                budget,
                ids).IsSuccess, Is.True);
            Assert.That(threat.AdvanceBeforeRelease(new TickIndex(1)).IsSuccess, Is.True);
            Assert.That(threat.TryCommitRelease(
                new TickIndex(2),
                budget,
                out ThreatRelease release).IsSuccess, Is.True);

            Assert.That(release.AttackId, Is.EqualTo(threat.AttackId));
            Assert.That(budget.ReservedUnits, Is.Zero);
            Assert.That(budget.ActiveUnits, Is.EqualTo(2));
            Assert.That(threat.TryCancelBeforeRelease(budget).IsSuccess, Is.False);
            Assert.That(threat.ConfirmPayloadsCreated(new TickIndex(2)).IsSuccess, Is.True);
            Assert.That(threat.State, Is.EqualTo(ThreatState.Recovery));
            Assert.That(threat.AdvanceBeforeRelease(new TickIndex(3)).IsSuccess, Is.True);
            Assert.That(threat.State, Is.EqualTo(ThreatState.Completed));
        }

        [Test]
        public void ProjectileTerminalStateCanOnlyBeCommittedOnce()
        {
            ProjectileRuntime projectile = CreateProjectile(interceptable: true);

            Assert.That(projectile.StartTravelling().IsSuccess, Is.True);
            Assert.That(projectile.TryHit().IsSuccess, Is.True);
            Assert.That(projectile.State, Is.EqualTo(ProjectileState.Hit));

            AssertAlreadyTerminal(projectile.TryHit());
            AssertAlreadyTerminal(projectile.TryCancel(new TickIndex(99)));
            AssertAlreadyTerminal(projectile.TryExpire(new TickIndex(99)));
        }

        [Test]
        public void DestroyedProjectileRejectsLaterDamageAndCannotHit()
        {
            ProjectileRuntime projectile = CreateProjectile(interceptable: true);
            projectile.StartTravelling();
            DamageResolver resolver = new DamageResolver(new ImpactLedger(4));

            ImpactIntent first = CreateProjectileImpact(
                new ImpactId(1),
                projectile,
                10);
            ImpactResolution destroyed = resolver.ResolveProjectile(first, projectile);
            Assert.That(destroyed.Result.IsSuccess, Is.True);
            Assert.That(destroyed.ProjectileDestroyed, Is.True);
            Assert.That(projectile.State, Is.EqualTo(ProjectileState.Destroyed));
            Assert.That(projectile.HitPoints, Is.Zero);

            ImpactResolution later = resolver.ResolveProjectile(
                CreateProjectileImpact(new ImpactId(2), projectile, 10),
                projectile);
            Assert.That(later.Result.IsSuccess, Is.False);
            Assert.That(later.Result.RejectReason, Is.EqualTo(RejectReason.InvalidTarget));
            AssertAlreadyTerminal(projectile.TryHit());
        }

        [Test]
        public void ProjectileBudgetMaintainsReservationAndActivationConservation()
        {
            ProjectileBudget budget = new ProjectileBudget(6);

            Assert.That(budget.TryReserve(3, out ReservationToken first).IsSuccess, Is.True);
            Assert.That(budget.TryReserve(2, out ReservationToken second).IsSuccess, Is.True);
            AssertConserved(budget);

            Assert.That(budget.Activate(first).IsSuccess, Is.True);
            Assert.That(budget.ReleaseReservation(second).IsSuccess, Is.True);
            Assert.That(budget.ReleaseActive(first, 1).IsSuccess, Is.True);
            AssertConserved(budget);
            Assert.That(budget.ReservedUnits, Is.Zero);
            Assert.That(budget.ActiveUnits, Is.EqualTo(2));

            Assert.That(budget.ReleaseActive(first, 2).IsSuccess, Is.True);
            AssertConserved(budget);
            Assert.That(budget.ActiveUnits, Is.Zero);
        }

        private static ProjectileRuntime CreateProjectile(bool interceptable)
        {
            ProjectileDefinition definition = new ProjectileDefinition(
                1,
                new TickDuration(3),
                new TickDuration(5),
                new DamageSpec(10, 0),
                10,
                interceptable,
                1);
            return new ProjectileRuntime(
                new ProjectileId(1),
                new RuntimeId(3),
                new AttackId(1),
                new RuntimeId(2),
                Team.Enemy,
                definition,
                new TickIndex(0),
                default(ReservationToken));
        }

        private static ImpactIntent CreateProjectileImpact(
            ImpactId impactId,
            ProjectileRuntime projectile,
            int damage)
        {
            return new ImpactIntent(
                impactId,
                new AttackId(99),
                new ShotId(99),
                new RuntimeId(1),
                projectile.RuntimeId,
                new TickIndex(1),
                new DamageSpec(damage, 0),
                HitPart.Projectile,
                DamageType.ProjectileIntercept,
                CombatTags.Primary);
        }

        private static void AssertAlreadyTerminal(DomainResult result)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.AlreadyTerminal));
        }

        private static void AssertConserved(ProjectileBudget budget)
        {
            Assert.That(budget.ReservedUnits, Is.GreaterThanOrEqualTo(0));
            Assert.That(budget.ActiveUnits, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                budget.ReservedUnits + budget.ActiveUnits,
                Is.LessThanOrEqualTo(budget.Capacity));
        }
    }
}
