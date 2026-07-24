using System;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0ActorAnimationStateMachineTests
    {
        private static readonly Assembly UnityAssembly = typeof(Actor2DPresenter).Assembly;
        private static readonly Type StateMachineType = UnityAssembly.GetType(
            "FPG.Demo.Unity.D0ActorAnimationStateMachine");
        private static readonly Type CommandType = UnityAssembly.GetType(
            "FPG.Demo.Unity.D0ActorAnimationCommand");

        [Test]
        public void PlayerChargeSurvivesShortReactionsAndCommittedPrimaryReturnsToIdle()
        {
            object stateMachine = CreateStateMachine(playerActor: true);

            Assert.That(Apply(stateMachine, "Initialize"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));
            Assert.That(Apply(stateMachine, "BeginSecondaryCharge"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("SecondaryCharging"));

            Assert.That(Apply(stateMachine, "Hit"), Is.True);
            Assert.That(Apply(stateMachine, "PlayerGroggy"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("SecondaryCharging"));

            Assert.That(Apply(stateMachine, "PrimaryAttack"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));
            Assert.That(Apply(stateMachine, "ReleaseSecondary"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));
        }

        [Test]
        public void PlayerLifeHitInterruptsReloadPresentation()
        {
            object stateMachine = CreateStateMachine(playerActor: true);

            Assert.That(Apply(stateMachine, "Initialize"), Is.True);
            Assert.That(Apply(stateMachine, "BeginReload"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Reloading"));

            Assert.That(Apply(stateMachine, "Hit"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));
        }

        [Test]
        public void TerminalPlayerStateRejectsFurtherTransitionsUntilReset()
        {
            object stateMachine = CreateStateMachine(playerActor: true);

            Assert.That(Apply(stateMachine, "Initialize"), Is.True);
            Assert.That(Apply(stateMachine, "PlayerVictory"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Victory"));
            Assert.That(Apply(stateMachine, "BeginSecondaryCharge"), Is.False);
            Assert.That(Apply(stateMachine, "Hit"), Is.False);

            Assert.That(Apply(stateMachine, "Reset"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));
            Assert.That(Apply(stateMachine, "BeginSecondaryCharge"), Is.True);
        }

        [Test]
        public void EnemyGroggyBlocksShortActionsUntilRecoveryOrReset()
        {
            object stateMachine = CreateStateMachine(playerActor: false);

            Assert.That(Apply(stateMachine, "Initialize"), Is.True);
            Assert.That(Apply(stateMachine, "EnemyGroggyStarted"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("EnemyGroggy"));
            Assert.That(Apply(stateMachine, "Hit"), Is.False);
            Assert.That(Apply(stateMachine, "EnemyFastThreat"), Is.False);
            Assert.That(Apply(stateMachine, "EnemyGroggyEnded"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));

            Assert.That(Apply(stateMachine, "EnemyDeath"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("EnemyDead"));
            Assert.That(Apply(stateMachine, "EnemyGroggyStarted"), Is.False);
            Assert.That(Apply(stateMachine, "Reset"), Is.True);
            Assert.That(StateName(stateMachine), Is.EqualTo("Idle"));
        }

        private static object CreateStateMachine(bool playerActor)
        {
            Assert.That(StateMachineType, Is.Not.Null);
            return Activator.CreateInstance(
                StateMachineType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { playerActor },
                culture: null);
        }

        private static bool Apply(object stateMachine, string commandName)
        {
            Assert.That(CommandType, Is.Not.Null);
            MethodInfo apply = StateMachineType.GetMethod(
                "TryApply",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            object command = Enum.Parse(CommandType, commandName);
            return (bool)apply.Invoke(stateMachine, new[] { command });
        }

        private static string StateName(object stateMachine)
        {
            PropertyInfo state = StateMachineType.GetProperty(
                "State",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(state, Is.Not.Null);
            object value = state.GetValue(stateMachine);
            Assert.That(value, Is.Not.Null);
            return value.ToString();
        }
    }
}
