using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    [Category("PlayerSkillExecution")]
    public sealed class FpgPlayerSkillExecutionControllerTests
    {
        [Test]
        public void WholeSequenceAmmoIsPreflightedBeforeTickZero()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 2,
                cooldownTicks: 0,
                ammoCost: 2,
                Event(10, 0, 101),
                Event(20, 2, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 3);

            DomainResult result = controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));
            Assert.That(
                player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotEnoughAmmo));
        }

        [Test]
        public void BoundSessionExecutionAllocatorContinuesAfterExternalConsumer()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)));
            FpgSkillExecutionIdAllocator executionIds =
                new FpgSkillExecutionIdAllocator();
            Assert.That(executionIds.Next().Value, Is.EqualTo(1L));
            Assert.That(
                controller.TryBindExecutionIdAllocator(
                    executionIds,
                    out string error),
                Is.True,
                error);

            PlayerRuntime player = CreatePlayer(magazineCapacity: 2);
            Assert.That(
                controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(0L),
                        true,
                        true),
                    player).IsSuccess,
                Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(
                controller.GetResult(0).RuntimeEvent.ExecutionId.Value,
                Is.EqualTo(2L));
            Assert.That(executionIds.Peek().Value, Is.EqualTo(3L));

            controller.Reset();
            Assert.That(executionIds.Peek().Value, Is.EqualTo(3L));
        }

        [Test]
        public void TickZeroAndEndpointAttacksConsumeAmmoPerCommittedEvent()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 2,
                cooldownTicks: 0,
                ammoCost: 1,
                Event(10, 0, 101),
                Event(20, 2, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            CommitPrimaryEvent(controller.GetResult(0), player, ids, release, 0L);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(1L), true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(2L), true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(20));
            CommitPrimaryEvent(controller.GetResult(0), player, ids, release, 2L);

            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(2));
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.PlannedLastAttackTick,
                Is.EqualTo(new TickIndex(2L)));
        }

        [Test]
        public void TypedActionIndexResolvesActionDuringExecution()
        {
            FpgCompiledSkillEvent actionEvent = new FpgCompiledSkillEvent(
                10,
                0,
                FpgSkillActionKind.Attack,
                0,
                targetSource: FpgSkillTargetSource.CurrentAim);
            FpgCompiledPlayerSkillDefinition primary =
                new FpgCompiledPlayerSkillDefinition(
                    new FpgCompiledSkillDefinition(
                        1,
                        new[]
                        {
                            new FpgCompiledSkillSequence(
                                FpgSkillSequenceKind.Execute,
                                0,
                                1001,
                                false,
                                new[] { actionEvent })
                        }),
                    0,
                    new[]
                    {
                        new FpgCompiledPlayerAttackAction(
                            FpgSkillAttackMode.PelletRays,
                            PelletPayload(101, 1))
                    },
                    Array.Empty<FpgCompiledPlayerProjectileAction>(),
                    Array.Empty<FpgCompiledPlayerReloadAction>());
            Assert.That(
                primary.TryResolveAction(
                    actionEvent,
                    out FpgCompiledPlayerSkillAction resolved),
                Is.True);
            Assert.That(resolved.Kind,
                Is.EqualTo(FpgPlayerSkillActionKind.PelletRay));

            FpgPlayerSkillExecutionController controller = CreateController(primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 2);
            Assert.That(
                controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(0L),
                        true,
                        true),
                    player).IsSuccess,
                Is.True);

            Assert.That(controller.ResultCount, Is.EqualTo(1));
            FpgPlayerSkillExecutionEvent result = controller.GetResult(0);
            Assert.That(result.HasGameplayAction, Is.True);
            Assert.That(result.Event.ActionKind,
                Is.EqualTo(FpgSkillActionKind.Attack));
            Assert.That(result.Event.ActionIndex, Is.Zero);
            Assert.That(result.Action.Kind,
                Is.EqualTo(FpgPlayerSkillActionKind.PelletRay));
            Assert.That(result.Action.AmmoCost, Is.EqualTo(1));
        }

        [Test]
        public void CooldownIsAnchoredToLastAttackAndNeverShorterThanSequence()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 5,
                cooldownTicks: 10,
                ammoCost: 1,
                Event(10, 2, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(100L), true, true),
                player).IsSuccess, Is.True);

            Assert.That(controller.PlannedLastAttackTick,
                Is.EqualTo(new TickIndex(102L)));
            Assert.That(controller.ActionLockedUntilTick,
                Is.EqualTo(new TickIndex(112L)));
            Assert.That(player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(112L)));
        }

        [Test]
        public void HardInterruptPreservesCooldownFromPlannedLastAttack()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 3,
                cooldownTicks: 4,
                ammoCost: 1,
                Event(10, 0, 101),
                Event(20, 2, 101),
                Event(30, 3, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);

            controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player);
            DomainResult interrupted = controller.HardInterrupt(
                new TickIndex(1L),
                player.Weapon);

            Assert.That(interrupted.IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(2));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(20));
            Assert.That(controller.GetResult(1).Event.EventId, Is.EqualTo(30));
            Assert.That(controller.GetResult(0).Outcome,
                Is.EqualTo(FpgSkillEventOutcome.Canceled));
            Assert.That(controller.GetResult(1).Outcome,
                Is.EqualTo(FpgSkillEventOutcome.Canceled));
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(
                player.Weapon.State,
                Is.EqualTo(WeaponState.PrimaryRecovery));
            Assert.That(
                controller.RecastLockedUntilTick,
                Is.EqualTo(new TickIndex(7L)));
            Assert.That(
                player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(7L)));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(
                controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));

            for (long tick = 1L; tick < 7L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(new TickIndex(tick), true, true),
                    player).IsSuccess, Is.True);
                Assert.That(controller.IsExecuting, Is.False);
            }

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(7L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(10));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
        }

        [Test]
        public void FailedGameplayAbortPreservesPlannedRecastCooldown()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 3,
                cooldownTicks: 4,
                ammoCost: 1,
                Event(10, 0, 101),
                Event(20, 3, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            PreparePrimaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                0L);
            Assert.That(release.Attack.AttackId, Is.EqualTo(new AttackId(1L)));
            Assert.That(release.Attack.ShotId, Is.EqualTo(new ShotId(1L)));

            release.Reset();
            controller.AbortAfterProcessedTick(player.Weapon);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(
                player.Weapon.State,
                Is.EqualTo(WeaponState.PrimaryRecovery));
            Assert.That(
                player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(7L)));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
            Assert.That(next.AttackId, Is.EqualTo(new AttackId(1L)));
            Assert.That(next.ShotId, Is.EqualTo(new ShotId(1L)));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(
                controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));

            for (long tick = 1L; tick < 7L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(new TickIndex(tick), true, true),
                    player).IsSuccess, Is.True);
                Assert.That(controller.IsExecuting, Is.False);
            }

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(7L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(10));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
        }

        [Test]
        public void CancelingChargeStartsTheAuthoredCancelSequence()
        {
            FpgCompiledPlayerSkillDefinition secondary =
                CreateChargeSecondary();
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                secondary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(
                    new InputSequence(1L),
                    InputEdgeType.SecondaryPressed)
            };

            Assert.That(controller.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(0L),
                    true,
                    false,
                    edges,
                    edges.Length,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);
            SkillExecutionId chargeExecutionId =
                controller.GetSequenceFrame(0).ExecutionId;

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(1L),
                    aimHeld: false,
                    cancelSecondary: true),
                player).IsSuccess, Is.True);

            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            FpgPlayerSkillSequenceFrame cancelFrame =
                controller.GetSequenceFrame(0);
            Assert.That(
                cancelFrame.Sequence.Kind,
                Is.EqualTo(FpgSkillSequenceKind.Cancel));
            Assert.That(
                cancelFrame.State,
                Is.EqualTo(FpgSkillExecutionState.Completed));
            Assert.That(
                cancelFrame.ExecutionId,
                Is.Not.EqualTo(chargeExecutionId));
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
        }

        [Test]
        public void SecondaryChargeWithInsufficientAmmoDoesNotStartPresentationTimeline()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary());
            PlayerRuntime player = CreatePlayer(magazineCapacity: 2);
            Assert.That(player.Weapon.Magazine.RestoreAmmo(0).IsSuccess, Is.True);
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(
                    new InputSequence(1L),
                    InputEdgeType.SecondaryPressed)
            };

            Assert.That(controller.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(0L),
                    true,
                    false,
                    edges,
                    edges.Length,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);

            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.SequenceFrameCount, Is.Zero);
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(player.Weapon.Magazine.Ammo, Is.Zero);
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotEnoughAmmo));
        }

        [Test]
        public void SequenceFramesResolveAnimationVariantFromExecutionId()
        {
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    0,
                    1001,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    0,
                    new[] { 1002 },
                    new[] { Event(10, 0, 101) });
            FpgCompiledPlayerSkillDefinition primary = Definition(
                1,
                0,
                sequence,
                PelletPayload(101, 1));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            FpgPlayerSkillSequenceFrame first =
                controller.GetSequenceFrame(0);
            Assert.That(first.ResolvedAnimationId, Is.EqualTo(1001));
            Assert.That(
                first.ResolvedAnimationId,
                Is.EqualTo(sequence.ResolveAnimation(first.ExecutionId)));
            CommitPrimaryEvent(controller.GetResult(0), player, ids, release, 0L);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(1L), true, true),
                player).IsSuccess, Is.True);
            FpgPlayerSkillSequenceFrame second =
                controller.GetSequenceFrame(0);
            Assert.That(second.ExecutionId, Is.Not.EqualTo(first.ExecutionId));
            Assert.That(second.ResolvedAnimationId, Is.EqualTo(1002));
            Assert.That(
                second.ResolvedAnimationId,
                Is.EqualTo(sequence.ResolveAnimation(second.ExecutionId)));
        }

                        private static FpgPlayerSkillExecutionController CreateController(
            FpgCompiledPlayerSkillDefinition primary)
        {
            return CreateController(primary, CreateSecondary());
        }

        private static FpgPlayerSkillExecutionController CreateController(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary)
        {
            bool created = FpgPlayerSkillExecutionController.TryCreate(
                primary,
                secondary,
                CreateReload(),
                SecondaryTriggerMode.ChargeRelease,
                out FpgPlayerSkillExecutionController controller,
                out string error);
            Assert.That(created, Is.True, error);
            return controller;
        }

        private static FpgCompiledPlayerSkillDefinition CreatePrimary(
            int durationTicks,
            int cooldownTicks,
            int ammoCost,
            params FpgCompiledSkillEvent[] events)
        {
            return Definition(
                1,
                cooldownTicks,
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    durationTicks,
                    1001,
                    false,
                    events),
                PelletPayload(101, ammoCost));
        }

        private static FpgCompiledPlayerSkillDefinition CreateSecondary()
        {
            return Definition(
                2,
                2,
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    0,
                    1002,
                    false,
                    new[] { Event(40, 0, 201) }),
                new FpgCompiledPlayerSkillAction(
                    FpgPlayerSkillActionKind.AreaAtFirstSurface,
                    1,
                    new DamageSpec(8, 2),
                    QueryPolicy.DirectThenArea,
                    AttackQueryMode.AreaAtFirstSurface,
                    1,
                    2,
                    0,
                    1,
                    1,
                    WeaponDefinition.PlayerAttackTargetKinds));
        }

        private static FpgCompiledPlayerSkillDefinition CreateChargeSecondary()
        {
            return new FpgCompiledPlayerSkillDefinition(
                new FpgCompiledSkillDefinition(
                    2,
                    new[]
                    {
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.Execute,
                            0,
                            2000,
                            false,
                            Array.Empty<FpgCompiledSkillEvent>()),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.ChargeEnter,
                            0,
                            2001,
                            false,
                            Array.Empty<FpgCompiledSkillEvent>()),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.ChargeLoop,
                            0,
                            2002,
                            true,
                            Array.Empty<FpgCompiledSkillEvent>()),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.Release,
                            0,
                            2003,
                            false,
                            new[] { Event(40, 0, 201) }),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.Cancel,
                            0,
                            2004,
                            false,
                            Array.Empty<FpgCompiledSkillEvent>())
                    }),
                2,
                new[]
                {
                    new FpgCompiledPlayerAttackAction(
                        FpgSkillAttackMode.AreaAtFirstSurface,
                        new FpgCompiledPlayerSkillAction(
                            FpgPlayerSkillActionKind.AreaAtFirstSurface,
                            1,
                            new DamageSpec(8, 2),
                            QueryPolicy.DirectThenArea,
                            AttackQueryMode.AreaAtFirstSurface,
                            1,
                            2,
                            0,
                            1,
                            1,
                            WeaponDefinition.PlayerAttackTargetKinds))
                },
                Array.Empty<FpgCompiledPlayerProjectileAction>(),
                Array.Empty<FpgCompiledPlayerReloadAction>());
        }

        private static FpgCompiledPlayerSkillDefinition CreateReload()
        {
            FpgCompiledSkillEvent reloadEvent = new FpgCompiledSkillEvent(
                50,
                1,
                FpgSkillActionKind.CommitReload,
                0,
                targetSource: FpgSkillTargetSource.Self);
            return Definition(
                3,
                0,
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    1,
                    1003,
                    false,
                    new[] { reloadEvent }),
                new FpgCompiledPlayerSkillAction(
                    FpgPlayerSkillActionKind.ReloadCommit,
                    0,
                    new DamageSpec(0, 0),
                    QueryPolicy.None,
                    AttackQueryMode.Legacy,
                    0,
                    0,
                    0,
                    0,
                    0,
                    AttackTargetKinds.None));
        }

        private static FpgCompiledPlayerSkillDefinition Definition(
            int skillId,
            int cooldownTicks,
            FpgCompiledSkillSequence sequence,
            FpgCompiledPlayerSkillAction payload)
        {
            FpgCompiledPlayerAttackAction[] attacks =
                Array.Empty<FpgCompiledPlayerAttackAction>();
            FpgCompiledPlayerReloadAction[] reloads =
                Array.Empty<FpgCompiledPlayerReloadAction>();
            if (payload.Kind == FpgPlayerSkillActionKind.ReloadCommit)
            {
                reloads = new[]
                {
                    new FpgCompiledPlayerReloadAction(payload)
                };
            }
            else
            {
                FpgSkillAttackMode mode = payload.Kind
                        == FpgPlayerSkillActionKind.PelletRay
                    ? FpgSkillAttackMode.PelletRays
                    : FpgSkillAttackMode.AreaAtFirstSurface;
                attacks = new[]
                {
                    new FpgCompiledPlayerAttackAction(mode, payload)
                };
            }

            return new FpgCompiledPlayerSkillDefinition(
                new FpgCompiledSkillDefinition(skillId, new[] { sequence }),
                cooldownTicks,
                attacks,
                System.Array.Empty<FpgCompiledPlayerProjectileAction>(),
                reloads);
        }

        private static FpgCompiledPlayerSkillAction PelletPayload(
            int slotId,
            int ammoCost)
        {
            return new FpgCompiledPlayerSkillAction(
                FpgPlayerSkillActionKind.PelletRay,
                ammoCost,
                new DamageSpec(4, 1),
                QueryPolicy.PelletRays,
                AttackQueryMode.FirstSurfacePenetration,
                1,
                1,
                0,
                0,
                0,
                WeaponDefinition.PlayerAttackTargetKinds);
        }

        private static FpgCompiledSkillEvent Event(
            int eventId,
            int tick,
            int actionToken,
            int sortOrder = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillActionKind.Attack,
                0,
                sortOrder: sortOrder,
                targetSource: FpgSkillTargetSource.CurrentAim);
        }

        private static PlayerRuntime CreatePlayer(int magazineCapacity)
        {
            WeaponDefinition weapon = new WeaponDefinition(
                900,
                magazineCapacity,
                1,
                new TickDuration(2),
                new DamageSpec(4, 1),
                1,
                TickDuration.Zero,
                new TickDuration(2),
                new DamageSpec(8, 2),
                new TickDuration(2),
                1,
                SecondaryTriggerMode.ChargeRelease,
                primaryPayloadCount: 1);
            return new PlayerRuntime(
                new CombatantState(
                    new RuntimeId(1L),
                    CombatantKind.Player,
                    100,
                    100,
                    0),
                new ExposureRuntime(),
                new WeaponRuntime(weapon));
        }

        private static void CommitPrimaryEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            PreparePrimaryEvent(skillEvent, player, ids, release, tick);
            Assert.That(player.Weapon.CommitPreparedSkillRelease(
                release,
                ids).IsSuccess, Is.True);
        }

        private static void PreparePrimaryEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            FpgCompiledPlayerSkillAction payload = skillEvent.Action;
            WeaponSkillReleaseSpec spec = new WeaponSkillReleaseSpec(
                WeaponReleaseKind.Primary,
                payload.Damage,
                payload.QueryPolicy,
                payload.QueryMode,
                payload.PayloadCount,
                payload.MaxImpactCount,
                payload.AmmoCost,
                payload.AdditionalPenetrationCount,
                payload.AreaCombatantLimit,
                payload.AreaProjectileLimit,
                payload.AllowedTargetKinds);
            Assert.That(player.Weapon.PrepareSkillRelease(
                new TickIndex(tick),
                player.RuntimeId,
                ids,
                123UL,
                spec,
                release).IsSuccess, Is.True);
        }
    }
}
