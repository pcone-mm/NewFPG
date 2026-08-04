using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgBattleTestSandboxRuntimeTests
    {
        [Test]
        public void EmptySandboxSessionRemainsRunning()
        {
            SandboxFixture fixture = new SandboxFixture(runtimeCapacity: 2);

            Assert.That(
                fixture.Session.Start(new TickIndex(0L)).IsSuccess,
                Is.True);
            for (int tick = 1; tick <= 4; tick++)
            {
                Assert.That(
                    fixture.Session.Advance(new TickIndex(tick)).IsSuccess,
                    Is.True,
                    "tick " + tick);
            }

            Assert.That(
                fixture.Session.State,
                Is.EqualTo(FpgEncounterSessionState.Running));
            Assert.That(
                fixture.Runtime.Phase,
                Is.EqualTo(FpgEncounterPhase.Combat));
            Assert.That(fixture.Runtime.IsTerminal, Is.False);
            Assert.That(fixture.EntityPort.PrepareCount, Is.Zero);
        }

        [Test]
        public void ExternalSpawnUsesSandboxPlacementAndFormalQueue()
        {
            SandboxFixture fixture = new SandboxFixture(runtimeCapacity: 2);
            Assert.That(
                fixture.Session.Start(new TickIndex(0L)).IsSuccess,
                Is.True);

            DomainResult queued = fixture.Session.TryQueueExternalSpawn(
                "burstbug",
                FpgSpawnPlacement.ForSandboxRoomPoint("enemy-any-02"),
                new TickIndex(0L),
                out RuntimeId runtimeId);

            Assert.That(queued.IsSuccess, Is.True);
            Assert.That(runtimeId.IsValid, Is.True);
            Assert.That(fixture.EntityPort.PrepareCount, Is.EqualTo(1));
            Assert.That(
                fixture.EntityPort.PreparedPlacements[0].Kind,
                Is.EqualTo(FpgSpawnPlacementKind.SandboxRoomPoint));
            Assert.That(
                fixture.EntityPort.PreparedPlacements[0].RoomPointId,
                Is.EqualTo("enemy-any-02"));
            Assert.That(fixture.SpawnResolver.ReserveCallCount, Is.Zero);

            Assert.That(
                fixture.Session.Advance(new TickIndex(1L)).IsSuccess,
                Is.True);
            Assert.That(fixture.EntityPort.ActivateCount, Is.EqualTo(1));
            Assert.That(fixture.Runtime.Roster.LivingCount, Is.EqualTo(1));
            Assert.That(
                fixture.Session.State,
                Is.EqualTo(FpgEncounterSessionState.Running));
        }

        [Test]
        public void ExternalSpawnBypassesGameplayCapButStopsAtFixedCapacity()
        {
            SandboxFixture fixture = new SandboxFixture(runtimeCapacity: 2);
            Assert.That(
                fixture.Session.Start(new TickIndex(0L)).IsSuccess,
                Is.True);

            DomainResult first = fixture.Session.TryQueueExternalSpawn(
                "burstbug",
                FpgSpawnPlacement.ForSandboxRoomPoint("enemy-any-01"),
                new TickIndex(0L),
                out _);
            DomainResult second = fixture.Session.TryQueueExternalSpawn(
                "burstbug",
                FpgSpawnPlacement.ForSandboxRoomPoint("enemy-any-01"),
                new TickIndex(0L),
                out _);
            DomainResult third = fixture.Session.TryQueueExternalSpawn(
                "burstbug",
                FpgSpawnPlacement.ForSandboxRoomPoint("enemy-any-01"),
                new TickIndex(0L),
                out _);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True,
                "The profile gameplay cap is one, but sandbox GM spawns bypass it.");
            Assert.That(third.IsSuccess, Is.False);
            Assert.That(third.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(fixture.EntityPort.PrepareCount, Is.EqualTo(2));
        }

        private sealed class SandboxFixture
        {
            public SandboxFixture(int runtimeCapacity)
            {
                Context = new FpgEncounterRunContext(
                    123UL,
                    "battle-test",
                    0,
                    FpgEncounterRunContext.BasisPointsOne,
                    0);
                Definition = new FpgEnemyDefinitionData(
                    "burstbug",
                    FpgEnemyRole.Any,
                    life: 10,
                    breakValue: 0,
                    spawnCost: 1,
                    capWeight: 1);
                Profile = new FpgEncounterProfileData(
                    baseBudget: 1,
                    depthRamp: 0,
                    minBudget: 1,
                    maxConcurrentCapWeight: 1,
                    maxConcurrentEntities: 1,
                    spawnIntervalTicks: 0,
                    warningDurationTicks: 0,
                    waveIntervalTicks: 0,
                    spawnSafetyDistanceUnits: 0,
                    entrySafetyDistanceUnits: 0,
                    maxSpawnWaitTicks: 0,
                    enemyRosterCapacity: runtimeCapacity,
                    threatCapacity: 1,
                    projectileCapacity: 1,
                    entityPoolCapacity: runtimeCapacity,
                    waveBudgetShares: new[]
                    {
                        new FpgWaveBudgetShare(
                            FpgEncounterRunContext.BasisPointsOne)
                    },
                    enemyPool: new[]
                    {
                        new FpgEnemyPoolEntryData(Definition, 1)
                    });
                Room = new RoomSource();
                ProfileSource = new ProfileSourceAdapter(Profile);
                FpgRoomRunRequest request = new FpgRoomRunRequest(
                    Room,
                    ProfileSource,
                    null,
                    Context);
                FpgEncounterPlan plan =
                    FpgEncounterPlanGenerator.CreateBattleTestSandbox(
                        Room.RoomDefinitionId,
                        Context);
                SpawnResolver = new RejectingSpawnResolver();
                EntityPort = new RecordingEntityPort();
                Runtime = new FpgEncounterRuntime(
                    plan,
                    Profile,
                    new FpgEnemyRoster(runtimeCapacity),
                    new SessionIdAllocator(),
                    new SingleDefinitionCatalog(Definition),
                    SpawnResolver,
                    EntityPort,
                    spawnQueueCapacity: runtimeCapacity,
                    mode: FpgEncounterRuntimeMode.BattleTestSandbox);
                Session = new FpgEncounterSession(request, Runtime);
            }

            public FpgEncounterRunContext Context { get; }
            public FpgEnemyDefinitionData Definition { get; }
            public FpgEncounterProfileData Profile { get; }
            public RoomSource Room { get; }
            public ProfileSourceAdapter ProfileSource { get; }
            public RejectingSpawnResolver SpawnResolver { get; }
            public RecordingEntityPort EntityPort { get; }
            public FpgEncounterRuntime Runtime { get; }
            public FpgEncounterSession Session { get; }
        }

        private sealed class RoomSource : IFpgRoomDefinitionSource
        {
            public string RoomDefinitionId => "battle-test-room";
            public int ExitCount => 0;
            public int SpawnPointCount => 1;

            public FpgSpawnPointCandidate GetSpawnPoint(int index)
            {
                Assert.That(index, Is.Zero);
                return new FpgSpawnPointCandidate(
                    "enemy-any-01",
                    FpgEnemyRole.Any,
                    1L);
            }
        }

        private sealed class ProfileSourceAdapter : IFpgEncounterProfileSource
        {
            public ProfileSourceAdapter(FpgEncounterProfileData data)
            {
                Data = data;
            }

            public FpgEncounterProfileData Data { get; }
        }

        private sealed class SingleDefinitionCatalog
            : IFpgEnemyDefinitionCatalog
        {
            private readonly FpgEnemyDefinitionData definition;

            public SingleDefinitionCatalog(FpgEnemyDefinitionData definition)
            {
                this.definition = definition;
            }

            public bool TryGet(
                string enemyDefinitionId,
                out FpgEnemyDefinitionData result)
            {
                bool found = enemyDefinitionId == definition.EnemyDefinitionId;
                result = found ? definition : null;
                return found;
            }
        }

        private sealed class RejectingSpawnResolver
            : IFpgEncounterSpawnPointResolver
        {
            public int ReserveCallCount { get; private set; }

            public DomainResult TryReserve(
                FpgSpawnEntry entry,
                FpgEncounterRunContext runContext,
                int attempt,
                out string pointId,
                out int relaxationLevel)
            {
                ReserveCallCount++;
                pointId = string.Empty;
                relaxationLevel = 0;
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            public void Release(string pointId, RuntimeId runtimeId)
            {
            }
        }

        private sealed class RecordingEntityPort : IFpgEncounterEntityPort
        {
            public List<FpgSpawnPlacement> PreparedPlacements { get; } =
                new List<FpgSpawnPlacement>();
            public int PrepareCount => PreparedPlacements.Count;
            public int ActivateCount { get; private set; }

            public DomainResult Prepare(
                FpgSpawnEntry entry,
                RuntimeId runtimeId,
                FpgSpawnPlacement placement)
            {
                PreparedPlacements.Add(placement);
                return DomainResult.Success;
            }

            public DomainResult Activate(
                FpgSpawnEntry entry,
                RuntimeId runtimeId,
                FpgSpawnPlacement placement)
            {
                ActivateCount++;
                return DomainResult.Success;
            }

            public DomainResult Despawn(
                RuntimeId runtimeId,
                bool preservePresentationLease)
            {
                return DomainResult.Success;
            }

            public void ClearAll()
            {
            }
        }
    }
}
