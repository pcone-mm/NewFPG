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

        [Test]
        public void CueCommitGateMatchesExactSameTickGameplayEventAndLeavesUnboundCueDirect()
        {
            FpgCompiledSkillEvent gameplayA = Event(10, 0, 101, 0);
            FpgCompiledSkillEvent gameplayB = Event(20, 0, 101, 1);
            FpgCompiledSkillEvent cueA =
                new FpgCompiledSkillEvent(
                    11,
                    0,
                    FpgSkillEventKind.PresentationCue,
                    0,
                    501,
                    0,
                    sortOrder: 2,
                    boundGameplayEventId: 10);
            FpgCompiledSkillEvent cueB =
                new FpgCompiledSkillEvent(
                    12,
                    0,
                    FpgSkillEventKind.PresentationCue,
                    0,
                    502,
                    0,
                    sortOrder: 3,
                    boundGameplayEventId: 20);
            FpgCompiledSkillEvent unboundCue =
                new FpgCompiledSkillEvent(
                    13,
                    0,
                    FpgSkillEventKind.PresentationCue,
                    0,
                    503,
                    0,
                    sortOrder: 4);
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 0,
                cooldownTicks: 0,
                ammoCost: 1,
                gameplayA,
                gameplayB,
                cueA,
                cueB,
                unboundCue);
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);

            FpgPlayerSkillExecutionEvent resolvedCueA =
                FindCue(controller, 501);
            Assert.That(
                FpgPlayerSkillPresentationCommitGate.TryResolveGameplayCommit(
                    controller,
                    resolvedCueA,
                    out FpgPlayerSkillExecutionEvent resolvedGameplayA),
                Is.True);
            Assert.That(resolvedGameplayA.Event.EventId, Is.EqualTo(10));
            Assert.That(
                FpgPlayerSkillPresentationCommitGate.RequiresGameplayCommit(
                    controller,
                    resolvedCueA),
                Is.True);

            FpgPlayerSkillExecutionEvent resolvedCueB =
                FindCue(controller, 502);
            Assert.That(
                FpgPlayerSkillPresentationCommitGate.TryResolveGameplayCommit(
                    controller,
                    resolvedCueB,
                    out FpgPlayerSkillExecutionEvent resolvedGameplayB),
                Is.True);
            Assert.That(resolvedGameplayB.Event.EventId, Is.EqualTo(20));
            Assert.That(
                FpgPlayerSkillPresentationCommitGate.RequiresGameplayCommit(
                    controller,
                    resolvedCueB),
                Is.True);

            FpgPlayerSkillExecutionEvent resolvedUnboundCue =
                FindCue(controller, 503);
            Assert.That(
                FpgPlayerSkillPresentationCommitGate.TryResolveGameplayCommit(
                    controller,
                    resolvedUnboundCue,
                    out FpgPlayerSkillExecutionEvent _),
                Is.False);
            Assert.That(
                FpgPlayerSkillPresentationCommitGate.RequiresGameplayCommit(
                    controller,
                    resolvedUnboundCue),
                Is.False);
        }

        [Test]
        public void AuthoredPresentationResolverMapsCompiledVariantAndCue()
        {
            const string assetPath =
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset";
            FpgPlayerSkillDefinition authored =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    assetPath);

            Assert.That(authored, Is.Not.Null, assetPath);
            Assert.That(
                authored.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiled,
                    out string error),
                Is.True,
                error);
            Assert.That(
                compiled.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence sequence),
                Is.True);

            int variantId = sequence.ResolveAnimation(
                new SkillExecutionId(2L));
            Assert.That(
                FpgPlayerSkillPresentationResolver.TryResolveAnimationName(
                    authored,
                    sequence.Kind,
                    variantId,
                    out string animationName),
                Is.True);
            Assert.That(animationName, Is.EqualTo("attack_play2"));

            FpgCompiledSkillEvent cue = default(FpgCompiledSkillEvent);
            bool foundCue = false;
            for (int index = 0; index < sequence.EventCount; index++)
            {
                if (sequence.GetEvent(index).Kind
                    == FpgSkillEventKind.PresentationCue)
                {
                    cue = sequence.GetEvent(index);
                    foundCue = true;
                    break;
                }
            }

            Assert.That(foundCue, Is.True);
            Assert.That(
                FpgPlayerSkillPresentationResolver.TryResolveCue(
                    authored,
                    sequence.Kind,
                    cue,
                    out FpgResolvedPlayerSkillCue resolvedCue),
                Is.True);
            Assert.That(
                resolvedCue.EventName,
                Is.EqualTo("cue.fei.primary.muzzle.0"));
            Assert.That(
                resolvedCue.CueName,
                Is.EqualTo("player.weapon.primary.muzzle"));
            Assert.That(
                resolvedCue.SocketName,
                Is.EqualTo("weapon.primary.muzzle"));
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
                new FpgCompiledPlayerSkillPayloadSlot(
                    201,
                    FpgPlayerSkillPayloadKind.AreaAtFirstSurface,
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
                    new FpgCompiledPlayerSkillPayloadSlot(
                        201,
                        FpgPlayerSkillPayloadKind.AreaAtFirstSurface,
                        1,
                        new DamageSpec(8, 2),
                        QueryPolicy.DirectThenArea,
                        AttackQueryMode.AreaAtFirstSurface,
                        1,
                        2,
                        0,
                        1,
                        1,
                        WeaponDefinition.PlayerAttackTargetKinds)
                });
        }

        private static FpgCompiledPlayerSkillDefinition CreateReload()
        {
            FpgCompiledSkillEvent reloadEvent = new FpgCompiledSkillEvent(
                50,
                1,
                FpgSkillEventKind.GameplayPayload,
                301,
                0,
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
                new FpgCompiledPlayerSkillPayloadSlot(
                    301,
                    FpgPlayerSkillPayloadKind.ReloadCommit,
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
            FpgCompiledPlayerSkillPayloadSlot payload)
        {
            return new FpgCompiledPlayerSkillDefinition(
                new FpgCompiledSkillDefinition(skillId, new[] { sequence }),
                cooldownTicks,
                new[] { payload });
        }

        private static FpgCompiledPlayerSkillPayloadSlot PelletPayload(
            int slotId,
            int ammoCost)
        {
            return new FpgCompiledPlayerSkillPayloadSlot(
                slotId,
                FpgPlayerSkillPayloadKind.PelletRay,
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
            int payloadSlotId,
            int sortOrder = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillEventKind.GameplayPayload,
                payloadSlotId,
                0,
                0,
                sortOrder: sortOrder,
                targetSource: FpgSkillTargetSource.CurrentAim);
        }

        private static FpgPlayerSkillExecutionEvent FindCue(
            FpgPlayerSkillExecutionController controller,
            int cueId)
        {
            for (int index = 0; index < controller.ResultCount; index++)
            {
                FpgPlayerSkillExecutionEvent candidate =
                    controller.GetResult(index);
                if (candidate.Event.Kind == FpgSkillEventKind.PresentationCue
                    && candidate.Event.CueId == cueId)
                {
                    return candidate;
                }
            }

            Assert.Fail("Expected presentation cue " + cueId + ".");
            return default(FpgPlayerSkillExecutionEvent);
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
            FpgCompiledPlayerSkillPayloadSlot payload = skillEvent.Payload;
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
