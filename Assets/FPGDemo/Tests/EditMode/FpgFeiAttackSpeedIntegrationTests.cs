using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    [Category("AttackTiming")]
    [Category("PlayerSkillExecution")]
    public sealed class FpgFeiAttackSpeedIntegrationTests
    {
        private const string CharacterPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Character.asset";

        [Test]
        public void BlockedDifferentAttackClearsImmediatelyWhenAmmoIsInsufficient()
        {
            Fixture fixture = CreateFixture(
                LoadCharacter().AttackSpeedProfile,
                StaticAttackSpeedBonusProvider.Zero);
            List<long> attackTicks = new List<long>();

            ProcessTick(fixture, 0L, primaryHeld: true);
            CommitAttacks(fixture, 0L, attackTicks);
            Assert.That(
                fixture.Player.Weapon.Magazine.RestoreAmmo(0).IsSuccess,
                Is.True);

            ProcessTick(
                fixture,
                1L,
                primaryHeld: true,
                suppliedFrame: SecondaryPressed(1L, 1L));

            Assert.That(fixture.Controller.HasPendingAttackIntent, Is.False);
            Assert.That(fixture.Controller.ActiveSlot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
        }

        [Test]
        public void LatestExplicitIntentReplacesAnOlderBufferedAttack()
        {
            Fixture fixture = CreateFixture(
                LoadCharacter().AttackSpeedProfile,
                StaticAttackSpeedBonusProvider.Zero);
            List<long> attackTicks = new List<long>();

            ProcessTick(fixture, 0L, primaryHeld: true);
            CommitAttacks(fixture, 0L, attackTicks);
            for (long tick = 1L; tick < 35L; tick++)
            {
                ProcessTick(fixture, tick, primaryHeld: true);
            }

            ProcessTick(
                fixture,
                35L,
                primaryHeld: true,
                suppliedFrame: SecondaryPressed(35L, 1L));
            ProcessTick(fixture, 36L, primaryHeld: false);
            ProcessTick(fixture, 37L, primaryHeld: true);

            Assert.That(fixture.Controller.HasPendingAttackIntent, Is.True);
            Assert.That(
                fixture.Controller.PendingAttackIntent.Slot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
            Assert.That(
                fixture.Controller.PendingAttackIntent.Source,
                Is.EqualTo(FpgAttackIntentSource.PrimaryPressed));
            Assert.That(
                fixture.Controller.PendingAttackIntent.IssuedTick,
                Is.EqualTo(new TickIndex(37L)));
        }

        [Test]
        public void FailedReplayPreflightDoesNotCancelActiveRecovery()
        {
            Fixture fixture = CreateFixture(
                new FpgAttackSpeedProfile(2.5d, 0d, 2.5d),
                StaticAttackSpeedBonusProvider.Zero);
            FpgSkillExecutionIdAllocator executionIds =
                new FpgSkillExecutionIdAllocator();
            Assert.That(
                fixture.Controller.TryBindExecutionIdAllocator(
                    executionIds,
                    out string bindError),
                Is.True,
                bindError);
            List<long> attackTicks = new List<long>();

            ProcessTick(fixture, 0L, primaryHeld: true);
            CommitAttacks(fixture, 0L, attackTicks);
            for (long tick = 1L; tick < 24L; tick++)
            {
                ProcessTick(fixture, tick, primaryHeld: true);
            }

            FieldInfo nextValue = typeof(FpgSkillExecutionIdAllocator)
                .GetField("nextValue", BindingFlags.Instance
                    | BindingFlags.NonPublic);
            Assert.That(nextValue, Is.Not.Null);
            nextValue.SetValue(executionIds, long.MaxValue);

            DomainResult result = fixture.Controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(24L),
                    aimHeld: true,
                    primaryHeld: true),
                fixture.Player);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.RejectReason,
                Is.EqualTo(RejectReason.InvalidDefinition));
            Assert.That(fixture.Controller.IsExecuting, Is.True);
            Assert.That(fixture.Controller.ActiveSlot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
            Assert.That(fixture.Controller.ActiveStartTick,
                Is.EqualTo(new TickIndex(0L)));
            Assert.That(fixture.Controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(
                fixture.Controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(fixture.Player.Weapon.State,
                Is.EqualTo(WeaponState.PrimaryRecovery));
        }

        private static Fixture CreateFixture(
            FpgAttackSpeedProfile profile,
            IAttackSpeedBonusProvider provider)
        {
            D0CharacterDefinition character = LoadCharacter();
            D0WeaponDefinition weaponAsset = character.Weapon;
            const SecondaryTriggerMode secondaryMode =
                SecondaryTriggerMode.ChargeRelease;
            Assert.That(
                weaponAsset.TryCompileSkills(
                    secondaryMode,
                    out FpgCompiledPlayerSkillDefinition primary,
                    out FpgCompiledPlayerSkillDefinition secondary,
                    out FpgCompiledPlayerSkillDefinition reload,
                    out string compileError),
                Is.True,
                compileError);
            Assert.That(
                weaponAsset.TryCreate(
                    secondaryMode,
                    out WeaponDefinition weaponDefinition,
                    out string weaponError),
                Is.True,
                weaponError);
            Assert.That(
                FpgPlayerSkillExecutionController.TryCreate(
                    primary,
                    secondary,
                    reload,
                    secondaryMode,
                    profile,
                    provider,
                    4,
                    out FpgPlayerSkillExecutionController controller,
                    out string controllerError),
                Is.True,
                controllerError);

            PlayerRuntime player = new PlayerRuntime(
                new CombatantState(
                    new RuntimeId(1L),
                    CombatantKind.Player,
                    100,
                    100,
                    0),
                new ExposureRuntime(),
                new WeaponRuntime(weaponDefinition));
            return new Fixture(controller, player);
        }

        private static D0CharacterDefinition LoadCharacter()
        {
            D0CharacterDefinition character =
                AssetDatabase.LoadAssetAtPath<D0CharacterDefinition>(
                    CharacterPath);
            Assert.That(character, Is.Not.Null, CharacterPath);
            Assert.That(character.TryValidate(out string error), Is.True, error);
            return character;
        }

        private static void ProcessTick(
            Fixture fixture,
            long tick,
            bool primaryHeld,
            PlayerInputFrame? suppliedFrame = null)
        {
            PlayerInputFrame frame = suppliedFrame
                ?? PlayerInputFrame.Empty(
                    new TickIndex(tick),
                    aimHeld: true,
                    primaryHeld: primaryHeld);
            DomainResult result = fixture.Controller.ProcessFrame(
                frame,
                fixture.Player);
            Assert.That(
                result.IsSuccess,
                Is.True,
                $"Tick {tick} failed: {result.RejectReason}");
        }

        private static PlayerInputFrame SecondaryPressed(
            long tick,
            long inputSequence)
        {
            InputEdgeCommand[] commands =
            {
                new InputEdgeCommand(
                    new InputSequence(inputSequence),
                    InputEdgeType.SecondaryPressed)
            };
            return new PlayerInputFrame(
                new TickIndex(tick),
                true,
                true,
                commands,
                commands.Length,
                secondaryHeld: true);
        }

        private static void CommitAttacks(
            Fixture fixture,
            long tick,
            List<long> attackTicks)
        {
            for (int index = 0;
                index < fixture.Controller.ResultCount;
                index++)
            {
                FpgPlayerSkillExecutionEvent skillEvent =
                    fixture.Controller.GetResult(index);
                if (!skillEvent.HasGameplayAction
                    || skillEvent.Action.Kind
                        != FpgPlayerSkillActionKind.PelletRay)
                {
                    continue;
                }

                FpgCompiledPlayerSkillAction action = skillEvent.Action;
                WeaponSkillReleaseSpec spec = new WeaponSkillReleaseSpec(
                    WeaponReleaseKind.Primary,
                    action.Damage,
                    action.QueryPolicy,
                    action.QueryMode,
                    action.PayloadCount,
                    action.MaxImpactCount,
                    action.AmmoCost,
                    action.AdditionalPenetrationCount,
                    action.AreaCombatantLimit,
                    action.AreaProjectileLimit,
                    action.AllowedTargetKinds);
                Assert.That(
                    fixture.Player.Weapon.PrepareSkillRelease(
                        new TickIndex(tick),
                        fixture.Player.RuntimeId,
                        fixture.Ids,
                        123UL,
                        spec,
                        fixture.Release).IsSuccess,
                    Is.True);
                Assert.That(
                    fixture.Player.Weapon.CommitPreparedSkillRelease(
                        fixture.Release,
                        fixture.Ids,
                        skillEvent.Timing.SameAttackReadyTick).IsSuccess,
                    Is.True);
                attackTicks.Add(tick);
            }
        }

        private sealed class Fixture
        {
            public Fixture(
                FpgPlayerSkillExecutionController controller,
                PlayerRuntime player)
            {
                Controller = controller;
                Player = player;
                InitialAmmo = player.Weapon.Magazine.Ammo;
                Ids = new SessionIdAllocator();
                Release = new WeaponReleaseBuffer();
            }

            public FpgPlayerSkillExecutionController Controller { get; }
            public PlayerRuntime Player { get; }
            public int InitialAmmo { get; }
            public SessionIdAllocator Ids { get; }
            public WeaponReleaseBuffer Release { get; }
        }

    }
}
