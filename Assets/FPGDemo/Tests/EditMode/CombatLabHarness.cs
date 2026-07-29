using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;

namespace FPG.Demo.Tests.EditMode
{
    internal static class CombatLabHarness
    {
        public static ScenarioDefinition CreateScenario(
            ulong seed = 0xD0UL,
            int projectileBudgetCapacity = 6,
            int projectileCapacity = 16,
            int threatCapacity = 8,
            int impactHistoryCapacity = 4096,
            int shotTargetHistoryCapacity = 1024,
            ThreatScheduleEntry[] threatSchedule = null,
            int secondaryMinimumChargeTicks = 0,
            EnemySpawnDefinition[] enemySpawns = null)
        {
            WeaponDefinition weapon = new WeaponDefinition(
                101,
                8,
                1,
                new TickDuration(3),
                new DamageSpec(10, 5, 15000, 20000),
                2,
                new TickDuration(secondaryMinimumChargeTicks),
                new TickDuration(5),
                new DamageSpec(24, 12, 15000, 20000),
                new TickDuration(12),
                8);

            return new ScenarioDefinition(
                seed,
                weapon,
                100,
                60,
                120,
                40,
                new TickDuration(18),
                5000,
                new TickDuration(20),
                projectileBudgetCapacity,
                projectileCapacity,
                threatCapacity,
                impactHistoryCapacity,
                shotTargetHistoryCapacity,
                threatSchedule,
                enemySpawns);
        }

        public static BattleSession CreateSession(
            ulong seed = 0xD0UL,
            IAttackResolutionPort attackResolutionPort = null,
            int projectileBudgetCapacity = 6,
            int projectileCapacity = 16,
            IProjectileWorldPort projectileWorldPort = null,
            int secondaryMinimumChargeTicks = 0)
        {
            return new BattleSessionFactory().Create(
                CreateScenario(
                    seed,
                    projectileBudgetCapacity,
                    projectileCapacity,
                    secondaryMinimumChargeTicks: secondaryMinimumChargeTicks),
                attackResolutionPort ?? new NullAttackResolutionPort(),
                null,
                projectileWorldPort ?? CreateProjectileWorldPort());
        }

        public static ScriptedProjectileWorldPort CreateProjectileWorldPort(
            ScriptedProjectileSweepMode sweepMode = ScriptedProjectileSweepMode.TargetAtArrival)
        {
            return new ScriptedProjectileWorldPort(sweepMode);
        }

        public static int PumpOneTick(
            BattleSession session,
            Func<TickIndex, PlayerInputFrame> frameFactory = null)
        {
            IPlayerInputSource source = new DelegateInputSource(
                frameFactory ?? (tick => PlayerInputFrame.Empty(tick)));
            long oneTickWallTime = DivideRoundUp(
                TimeSpan.TicksPerSecond,
                GameplayClock.DefaultTickRate);

            DomainResult result = session.Pump(oneTickWallTime, source, out int executedSteps);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("BattleSession pump failed: " + result);
            }

            return executedSteps;
        }

        public static void PumpTicks(
            BattleSession session,
            int count,
            Func<TickIndex, PlayerInputFrame> frameFactory = null)
        {
            for (int index = 0; index < count; index++)
            {
                int executed = PumpOneTick(session, frameFactory);
                if (executed != 1)
                {
                    throw new InvalidOperationException("Expected one gameplay step but executed " + executed + ".");
                }
            }
        }

        public static ThreatDefinition CreateThreatDefinition(
            int payloadCount = 1,
            int projectileBudgetUnits = 1,
            int projectileHitPoints = 10,
            bool interceptable = true,
            int projectileDamage = 20,
            int telegraphTicks = 1,
            int windupTicks = 1,
            int flightTicks = 3,
            int recoveryTicks = 1)
        {
            ProjectileDefinition projectile = new ProjectileDefinition(
                301,
                new TickDuration(flightTicks),
                new TickDuration(flightTicks + 2),
                new DamageSpec(projectileDamage, 0),
                projectileHitPoints,
                interceptable,
                projectileBudgetUnits);

            return new ThreatDefinition(
                201,
                new TickDuration(telegraphTicks),
                new TickDuration(windupTicks),
                new TickDuration(recoveryTicks),
                projectile,
                payloadCount,
                interceptable
                    ? FpgThreatPresentationKind.InterceptableVolley
                    : FpgThreatPresentationKind.FastUninterceptable);
        }

        private static long DivideRoundUp(long numerator, long denominator)
        {
            return (numerator + denominator - 1L) / denominator;
        }

        private sealed class DelegateInputSource : IPlayerInputSource
        {
            private readonly Func<TickIndex, PlayerInputFrame> frameFactory;

            public DelegateInputSource(Func<TickIndex, PlayerInputFrame> frameFactory)
            {
                this.frameFactory = frameFactory;
            }

            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return frameFactory(tick);
            }
        }
    }
}
