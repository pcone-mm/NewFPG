using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    public sealed class ScenarioDefinition
    {
        public ScenarioDefinition(
            ulong scenarioSeed,
            WeaponDefinition playerWeapon,
            int playerLife,
            int playerBarrier,
            int enemyLife,
            int enemyBreak,
            TickDuration perfectRetractWindow,
            int perfectRetractMultiplierBasisPoints,
            TickDuration enemyGroggyDuration,
            int projectileBudgetCapacity,
            int projectileCapacity = 32,
            int threatCapacity = 8,
            int impactHistoryCapacity = 4096,
            int shotTargetHistoryCapacity = 1024,
            ThreatScheduleEntry[] threatSchedule = null,
            EnemySpawnDefinition[] enemySpawns = null)
        {
            if (playerLife <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLife));
            }

            if (playerBarrier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerBarrier));
            }

            if (enemyLife <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyLife));
            }

            if (enemyBreak <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyBreak));
            }

            if (perfectRetractWindow.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perfectRetractWindow));
            }

            if (perfectRetractMultiplierBasisPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perfectRetractMultiplierBasisPoints));
            }

            if (enemyGroggyDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyGroggyDuration));
            }

            if (projectileBudgetCapacity <= 0
                || projectileCapacity <= 0
                || threatCapacity <= 0
                || impactHistoryCapacity <= 0
                || shotTargetHistoryCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileBudgetCapacity));
            }

            ScenarioSeed = scenarioSeed;
            PlayerWeapon = playerWeapon;
            PlayerLife = playerLife;
            PlayerBarrier = playerBarrier;
            EnemyLife = enemyLife;
            EnemyBreak = enemyBreak;
            PerfectRetractWindow = perfectRetractWindow;
            PerfectRetractMultiplierBasisPoints = perfectRetractMultiplierBasisPoints;
            EnemyGroggyDuration = enemyGroggyDuration;
            ProjectileBudgetCapacity = projectileBudgetCapacity;
            ProjectileCapacity = projectileCapacity;
            ThreatCapacity = threatCapacity;
            ImpactHistoryCapacity = impactHistoryCapacity;
            ShotTargetHistoryCapacity = shotTargetHistoryCapacity;
            ThreatSchedule = CopyAndSortThreatSchedule(threatSchedule);
            EnemySpawns = CopyAndValidateEnemySpawns(enemySpawns);
            ValidateEnemySpawnCapacity(EnemySpawns, threatCapacity);
            ValidateThreatScheduleCapacity(
                ThreatSchedule,
                projectileBudgetCapacity,
                projectileCapacity);
            DefinitionHash = ComputeDefinitionHash();
        }

        public ulong ScenarioSeed { get; }
        public WeaponDefinition PlayerWeapon { get; }
        public int PlayerLife { get; }
        public int PlayerBarrier { get; }
        public int EnemyLife { get; }
        public int EnemyBreak { get; }
        public TickDuration PerfectRetractWindow { get; }
        public int PerfectRetractMultiplierBasisPoints { get; }
        public TickDuration EnemyGroggyDuration { get; }
        public int ProjectileBudgetCapacity { get; }
        public int ProjectileCapacity { get; }
        public int ThreatCapacity { get; }
        public int ImpactHistoryCapacity { get; }
        public int ShotTargetHistoryCapacity { get; }
        public int ThreatScheduleCount => ThreatSchedule.Length;
        public int EnemySpawnCount => EnemySpawns.Length;
        public ulong DefinitionHash { get; }

        public ThreatScheduleEntry GetThreatScheduleEntry(int index)
        {
            if (index < 0 || index >= ThreatSchedule.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ThreatSchedule[index];
        }

        public EnemySpawnDefinition GetEnemySpawnDefinition(int index)
        {
            if (index < 0 || index >= EnemySpawns.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return EnemySpawns[index];
        }

        private ThreatScheduleEntry[] ThreatSchedule { get; }
        private EnemySpawnDefinition[] EnemySpawns { get; }

        private ulong ComputeDefinitionHash()
        {
            ulong hash = StableHash.Mix(0x4650475F53434E31UL);
            hash = StableHash.Append(hash, unchecked((ulong)PlayerLife));
            hash = StableHash.Append(hash, unchecked((ulong)PlayerBarrier));
            hash = StableHash.Append(hash, unchecked((ulong)EnemyLife));
            hash = StableHash.Append(hash, unchecked((ulong)EnemyBreak));
            hash = StableHash.Append(hash, unchecked((ulong)PerfectRetractWindow.Value));
            hash = StableHash.Append(hash, unchecked((ulong)PerfectRetractMultiplierBasisPoints));
            hash = StableHash.Append(hash, unchecked((ulong)EnemyGroggyDuration.Value));
            hash = StableHash.Append(hash, unchecked((ulong)ProjectileBudgetCapacity));
            hash = StableHash.Append(hash, unchecked((ulong)ProjectileCapacity));
            hash = StableHash.Append(hash, unchecked((ulong)ThreatCapacity));
            hash = StableHash.Append(hash, unchecked((ulong)ImpactHistoryCapacity));
            hash = StableHash.Append(hash, unchecked((ulong)ShotTargetHistoryCapacity));
            hash = StableHash.Append(hash, unchecked((ulong)ThreatSchedule.Length));
            for (int index = 0; index < ThreatSchedule.Length; index++)
            {
                hash = ThreatSchedule[index].AppendStableHash(hash);
            }

            hash = StableHash.Append(hash, unchecked((ulong)EnemySpawns.Length));
            for (int index = 0; index < EnemySpawns.Length; index++)
            {
                EnemySpawnDefinition spawn = EnemySpawns[index];
                hash = StableHash.Append(hash, unchecked((ulong)spawn.DefinitionId));
                hash = StableHash.Append(hash, unchecked((ulong)spawn.SpawnTick.Value));
                hash = StableHash.Append(hash, unchecked((ulong)spawn.Life));
                hash = StableHash.Append(hash, unchecked((ulong)spawn.Break));
                hash = StableHash.Append(hash, unchecked((ulong)spawn.GroggyDuration.Value));
                hash = StableHash.Append(hash, unchecked((ulong)spawn.ThreatCapacity));
            }

            hash = AppendWeaponDefinition(hash, PlayerWeapon);
            return hash;
        }

        private static ThreatScheduleEntry[] CopyAndSortThreatSchedule(ThreatScheduleEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<ThreatScheduleEntry>();
            }

            ThreatScheduleEntry[] copy = new ThreatScheduleEntry[source.Length];
            Array.Copy(source, copy, source.Length);
            for (int index = 0; index < copy.Length; index++)
            {
                if (!copy[index].IsValid)
                {
                    throw new ArgumentException("Threat schedule entries must be valid.", nameof(source));
                }
            }

            for (int index = 1; index < copy.Length; index++)
            {
                ThreatScheduleEntry candidate = copy[index];
                int position = index - 1;
                while (position >= 0 && CompareSchedule(copy[position], candidate) > 0)
                {
                    copy[position + 1] = copy[position];
                    position--;
                }

                copy[position + 1] = candidate;
            }

            for (int index = 0; index < copy.Length; index++)
            {
                for (int other = index + 1; other < copy.Length; other++)
                {
                    if (copy[index].ScheduleSequence == copy[other].ScheduleSequence)
                    {
                        throw new ArgumentException("Threat schedule sequence values must be unique.", nameof(source));
                    }
                }
            }

            return copy;
        }

        private static EnemySpawnDefinition[] CopyAndValidateEnemySpawns(
            EnemySpawnDefinition[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<EnemySpawnDefinition>();
            }

            EnemySpawnDefinition[] copy = new EnemySpawnDefinition[source.Length];
            Array.Copy(source, copy, source.Length);
            long previousTick = 0L;
            for (int index = 0; index < copy.Length; index++)
            {
                EnemySpawnDefinition spawn = copy[index];
                if (spawn.DefinitionId <= 1
                    || !spawn.SpawnTick.IsValid
                    || spawn.SpawnTick.Value <= previousTick
                    || spawn.Life <= 0
                    || spawn.Break < 0
                    || spawn.GroggyDuration.Value <= 0
                    || spawn.ThreatCapacity <= 0)
                {
                    throw new ArgumentException(
                        "Enemy spawn definitions must use an id greater than the initial enemy id (1), be valid, and be ordered by increasing spawn tick.",
                        nameof(source));
                }

                for (int prior = 0; prior < index; prior++)
                {
                    if (copy[prior].DefinitionId == spawn.DefinitionId)
                    {
                        throw new ArgumentException(
                            "Enemy spawn definition ids must be unique.",
                            nameof(source));
                    }
                }

                previousTick = spawn.SpawnTick.Value;
            }

            return copy;
        }

        private static void ValidateEnemySpawnCapacity(
            EnemySpawnDefinition[] spawns,
            int threatCapacity)
        {
            for (int index = 0; index < spawns.Length; index++)
            {
                if (spawns[index].ThreatCapacity > threatCapacity)
                {
                    throw new ArgumentException(
                        "Enemy spawn threat capacity cannot exceed scenario threat capacity.",
                        nameof(spawns));
                }
            }
        }

        private static int CompareSchedule(ThreatScheduleEntry left, ThreatScheduleEntry right)
        {
            int tick = left.DueTick.CompareTo(right.DueTick);
            return tick != 0 ? tick : left.ScheduleSequence.CompareTo(right.ScheduleSequence);
        }

        private static void ValidateThreatScheduleCapacity(
            ThreatScheduleEntry[] schedule,
            int projectileBudgetCapacity,
            int projectileCapacity)
        {
            for (int index = 0; index < schedule.Length; index++)
            {
                ThreatPayloadDefinition payload = schedule[index].Payload;
                if (payload.IsSweptProjectile
                    && (payload.PayloadCount > projectileCapacity
                        || payload.TotalBudgetUnits > projectileBudgetCapacity))
                {
                    throw new ArgumentException(
                        "A swept projectile schedule entry exceeds scenario projectile capacity.",
                        nameof(schedule));
                }
            }
        }

        private static ulong AppendWeaponDefinition(ulong hash, WeaponDefinition weapon)
        {
            hash = StableHash.Append(hash, unchecked((ulong)weapon.DefinitionId));
            hash = StableHash.Append(hash, unchecked((ulong)weapon.MagazineCapacity));
            hash = StableHash.Append(hash, unchecked((ulong)weapon.PrimaryAmmoCost));
            hash = StableHash.Append(hash, unchecked((ulong)weapon.PrimaryInterval.Value));
            hash = AppendDamageSpec(hash, weapon.PrimaryDamage);
            hash = StableHash.Append(hash, (ulong)weapon.PrimaryQueryMode);
            hash = StableHash.Append(
                hash,
                unchecked((ulong)weapon.PrimaryAdditionalPenetrationCount));
            hash = StableHash.Append(hash, (ulong)weapon.PrimaryAllowedTargetKinds);
            hash = StableHash.Append(hash, unchecked((ulong)weapon.SecondaryAmmoCost));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)weapon.SecondaryMinimumCharge.Value));
            hash = StableHash.Append(hash, unchecked((ulong)weapon.SecondaryRecovery.Value));
            hash = AppendDamageSpec(hash, weapon.SecondaryDamage);
            hash = StableHash.Append(hash, unchecked((ulong)weapon.ReloadDuration.Value));
            hash = StableHash.Append(hash, unchecked((ulong)weapon.SecondaryMaxImpactCount));
            hash = StableHash.Append(
                hash,
                unchecked((ulong)weapon.SecondaryAreaProjectileLimit));
            hash = StableHash.Append(hash, (ulong)weapon.SecondaryQueryMode);
            hash = StableHash.Append(hash, (ulong)weapon.SecondaryAllowedTargetKinds);
            return StableHash.Append(hash, (ulong)weapon.SecondaryTriggerMode);
        }

        internal static ulong AppendThreatDefinition(ulong hash, ThreatDefinition threat)
        {
            hash = StableHash.Append(hash, unchecked((ulong)threat.DefinitionId));
            hash = StableHash.Append(hash, unchecked((ulong)threat.TelegraphDuration.Value));
            hash = StableHash.Append(hash, unchecked((ulong)threat.WindupDuration.Value));
            hash = StableHash.Append(hash, unchecked((ulong)threat.RecoveryDuration.Value));
            return threat.Payload.AppendStableHash(hash);
        }

        private static ulong AppendDamageSpec(ulong hash, DamageSpec damage)
        {
            hash = StableHash.Append(hash, unchecked((ulong)damage.BaseDamage));
            hash = StableHash.Append(hash, unchecked((ulong)damage.BreakDamage));
            hash = StableHash.Append(hash, unchecked((ulong)damage.WeakpointDamageMultiplierBasisPoints));
            return StableHash.Append(hash, unchecked((ulong)damage.WeakpointBreakMultiplierBasisPoints));
        }
    }

    public sealed class BattleSessionFactory
    {
        public BattleSession Create(ScenarioDefinition definition, IAttackResolutionPort attackResolutionPort)
        {
            return Create(definition, attackResolutionPort, null, null);
        }

        public BattleSession Create(
            ScenarioDefinition definition,
            IAttackResolutionPort attackResolutionPort,
            IAttackQueryPort attackQueryPort,
            IProjectileWorldPort projectileWorldPort)
        {
            return Create(
                definition,
                attackResolutionPort,
                attackQueryPort,
                projectileWorldPort,
                null);
        }

        public BattleSession Create(
            ScenarioDefinition definition,
            IAttackResolutionPort attackResolutionPort,
            IAttackQueryPort attackQueryPort,
            IProjectileWorldPort projectileWorldPort,
            ISpatialDigestView spatialDecisionView,
            ICommittedPlayerShotPresentationSink committedPlayerShotPresentationSink = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            SessionIdAllocator idAllocator = new SessionIdAllocator();
            int maxHits = definition.PlayerWeapon.MaximumAttackImpactCount;
            int impactQueueCapacity = checked(
                maxHits
                + definition.ProjectileCapacity
                + definition.ThreatCapacity);
            CombatKernel combatKernel = new CombatKernel(
                definition.ProjectileBudgetCapacity,
                impactCapacity: definition.ImpactHistoryCapacity,
                shotTargetCapacity: definition.ShotTargetHistoryCapacity,
                impactQueueCapacity: impactQueueCapacity,
                projectileReservationCapacity: checked(
                    definition.ProjectileCapacity + definition.ThreatCapacity));

            CombatantState playerCombatant = new CombatantState(
                idAllocator.NextRuntimeId(),
                CombatantKind.Player,
                definition.PlayerLife,
                definition.PlayerBarrier,
                0);
            PlayerRuntime player = new PlayerRuntime(
                playerCombatant,
                new ExposureRuntime(),
                new WeaponRuntime(definition.PlayerWeapon));

            CombatantState enemyCombatant = new CombatantState(
                idAllocator.NextRuntimeId(),
                CombatantKind.Enemy,
                definition.EnemyLife,
                0,
                definition.EnemyBreak);
            EnemyRuntime enemy = new EnemyRuntime(
                enemyCombatant,
                definition.EnemyGroggyDuration,
                definition.ThreatCapacity);

            return new BattleSession(
                definition,
                new GameplayClock(),
                idAllocator,
                combatKernel,
                player,
                enemy,
                attackResolutionPort ?? new NullAttackResolutionPort(),
                attackQueryPort ?? new NullAttackQueryPort(),
                projectileWorldPort ?? new NullProjectileWorldPort(),
                spatialDecisionView,
                committedPlayerShotPresentationSink);
        }

        public BattleSession Restart(BattleSession previous, IAttackResolutionPort attackResolutionPort)
        {
            if (previous == null)
            {
                throw new ArgumentNullException(nameof(previous));
            }

            return Restart(
                previous,
                attackResolutionPort,
                null,
                null,
                null);
        }

        public BattleSession Restart(
            BattleSession previous,
            IAttackResolutionPort attackResolutionPort,
            IAttackQueryPort attackQueryPort,
            IProjectileWorldPort projectileWorldPort)
        {
            return Restart(
                previous,
                attackResolutionPort,
                attackQueryPort,
                projectileWorldPort,
                null);
        }

        public BattleSession Restart(
            BattleSession previous,
            IAttackResolutionPort attackResolutionPort,
            IAttackQueryPort attackQueryPort,
            IProjectileWorldPort projectileWorldPort,
            ISpatialDigestView spatialDecisionView)
        {
            if (previous == null)
            {
                throw new ArgumentNullException(nameof(previous));
            }

            ScenarioDefinition definition = previous.Definition;
            previous.DisposeForRestart();
            return Create(
                definition,
                attackResolutionPort,
                attackQueryPort,
                projectileWorldPort,
                spatialDecisionView);
        }
    }
}
