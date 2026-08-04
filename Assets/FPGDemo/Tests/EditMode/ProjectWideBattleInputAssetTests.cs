using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class ProjectWideBattleInputAssetTests
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void BattleMapUsesRmbForAimAndSecondary()
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(actions, Is.Not.Null);

            InputActionMap battle = actions.FindActionMap("Battle", throwIfNotFound: true);
            Assert.That(battle.actions.Count, Is.EqualTo(6));
            AssertBattleBinding(battle, "Aim", "<Mouse>/rightButton");
            AssertBattleBinding(battle, "Primary", "<Mouse>/leftButton");
            AssertBattleBinding(battle, "Secondary", "<Mouse>/rightButton");
            AssertBattleBinding(battle, "Reload", "<Keyboard>/r");
            AssertBattleBinding(battle, "Pause", "<Keyboard>/escape");
            AssertBattleBinding(battle, "Restart", "<Keyboard>/f5");
        }

        [Test]
        public void AimSurfaceUsesTheProjectWideAimLookAndPointActions()
        {
            InputActionAsset actions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(actions, Is.Not.Null);

            InputAction aim = actions.FindActionMap(
                "Battle",
                throwIfNotFound: true).FindAction(
                    "Aim",
                    throwIfNotFound: true);
            InputAction look = actions.FindActionMap(
                "Player",
                throwIfNotFound: true).FindAction(
                    "Look",
                    throwIfNotFound: true);
            InputAction point = actions.FindActionMap(
                "UI",
                throwIfNotFound: true).FindAction(
                    "Point",
                    throwIfNotFound: true);

            Assert.That(aim.type, Is.EqualTo(InputActionType.Button));
            Assert.That(look.type, Is.EqualTo(InputActionType.Value));
            Assert.That(look.expectedControlType, Is.EqualTo("Vector2"));
            Assert.That(point.type, Is.EqualTo(InputActionType.PassThrough));
            Assert.That(point.expectedControlType, Is.EqualTo("Vector2"));
            AssertHasBinding(look, "<Gamepad>/rightStick");
            AssertHasBinding(look, "<Pointer>/delta");
            AssertHasBinding(point, "<Mouse>/position");
        }

        [Test]
        public void CoverMovementReusesPlayerMoveHorizontalKeyboardBindings()
        {
            InputActionAsset actions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(actions, Is.Not.Null);

            InputAction move = actions.FindActionMap(
                "Player",
                throwIfNotFound: true).FindAction(
                    "Move",
                    throwIfNotFound: true);
            Assert.That(move.type, Is.EqualTo(InputActionType.Value));
            Assert.That(move.expectedControlType, Is.EqualTo("Vector2"));
            AssertHasBinding(move, "<Keyboard>/a");
            AssertHasBinding(move, "<Keyboard>/leftArrow");
            AssertHasBinding(move, "<Keyboard>/d");
            AssertHasBinding(move, "<Keyboard>/rightArrow");
        }

        [Test]
        public void AimInputUsesLastEffectiveDeviceWithoutAddingChannels()
        {
            InputActionAsset source =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(source, Is.Not.Null, InputActionsPath);

            InputActionAsset previousActions = InputSystem.actions;
            bool sourceWasEnabled = source.enabled;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            ProjectWideBattleInputAdapter adapter =
                new ProjectWideBattleInputAdapter();
            try
            {
                InputSystem.actions = source;
                source.Enable();
                Assert.That(
                    adapter.TryReadAimInput(
                        0.2f,
                        out ProjectWideAimInputSnapshot initial),
                    Is.True);

                InputSystem.QueueDeltaStateEvent(
                    mouse.delta,
                    new Vector2(12f, 0f));
                InputSystem.QueueStateEvent(
                    gamepad,
                    new GamepadState
                    {
                        rightStick = new Vector2(0.6f, 0f)
                    });
                InputSystem.Update();

                Assert.That(
                    adapter.TryReadAimInput(
                        0.2f,
                        out ProjectWideAimInputSnapshot gamepadLast),
                    Is.True);
                Assert.That(
                    gamepadLast.InputChannel,
                    Is.EqualTo(ProjectWideAimInputChannel.Gamepad));
                Assert.That(
                    gamepadLast.LookDelta,
                    Is.EqualTo(gamepad.rightStick.ReadValue()));

                InputSystem.QueueStateEvent(
                    gamepad,
                    new GamepadState
                    {
                        rightStick = new Vector2(0.55f, 0f)
                    });
                InputSystem.QueueDeltaStateEvent(
                    mouse.delta,
                    new Vector2(9f, 0f));
                InputSystem.Update();

                Assert.That(
                    adapter.TryReadAimInput(
                        0.2f,
                        out ProjectWideAimInputSnapshot mouseLast),
                    Is.True);
                Assert.That(
                    mouseLast.InputChannel,
                    Is.EqualTo(ProjectWideAimInputChannel.Mouse));
                Assert.That(mouseLast.LookDelta.x, Is.EqualTo(9f).Within(0.001f));

                InputSystem.QueueDeltaStateEvent(
                    mouse.delta,
                    new Vector2(7f, 0f));
                InputSystem.QueueStateEvent(
                    gamepad,
                    new GamepadState
                    {
                        rightStick = new Vector2(0.05f, 0f)
                    });
                InputSystem.Update();

                Assert.That(
                    adapter.TryReadAimInput(
                        0.2f,
                        out ProjectWideAimInputSnapshot driftIgnored),
                    Is.True);
                Assert.That(
                    driftIgnored.InputChannel,
                    Is.EqualTo(ProjectWideAimInputChannel.Mouse));
                Assert.That(driftIgnored.LookDelta.x, Is.EqualTo(7f).Within(0.001f));
            }
            finally
            {
                adapter.Dispose();
                if (!sourceWasEnabled)
                {
                    source.Disable();
                }

                InputSystem.actions = previousActions;
                InputSystem.RemoveDevice(gamepad);
                InputSystem.RemoveDevice(mouse);
            }
        }


        [Test]
        public void AttackPressSnapshotReportsOnlyPressEdges()
        {
            InputActionAsset source =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    InputActionsPath);
            Assert.That(source, Is.Not.Null, InputActionsPath);

            InputActionAsset previousActions = InputSystem.actions;
            InputSettings previousSettings = InputSystem.settings;
            InputSettings testSettings =
                Object.Instantiate(previousSettings);
            testSettings.SetInternalFeatureFlag(
                "RUN_PLAYER_UPDATES_IN_EDIT_MODE",
                true);
            bool sourceWasEnabled = source.enabled;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            var previousDevices = source.devices;
            ProjectWideBattleInputAdapter adapter =
                new ProjectWideBattleInputAdapter();
            try
            {
                InputSystem.settings = testSettings;
                InputSystem.actions = source;
                source.devices = new InputDevice[] { mouse };
                source.Enable();

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Assert.That(
                    adapter.TryReadAttackPresses(
                        out ProjectWideAttackPressSnapshot initial),
                    Is.True,
                    "initial adapter read");
                Assert.That(initial.AnyAttackPressed, Is.False);

                MouseState simultaneousState = new MouseState()
                    .WithButton(MouseButton.Left)
                    .WithButton(MouseButton.Right);
                InputSystem.QueueStateEvent(mouse, simultaneousState);
                InputSystem.Update();
                Assert.That(
                    adapter.TryReadAttackPresses(
                        out ProjectWideAttackPressSnapshot simultaneous),
                    Is.True,
                    "simultaneous adapter read");
                Assert.That(
                    simultaneous.PrimaryPressed,
                    Is.True,
                    "simultaneous primary edge");
                Assert.That(
                    simultaneous.SecondaryPressed,
                    Is.True,
                    "simultaneous secondary edge");

                InputSystem.Update();
                Assert.That(
                    adapter.TryReadAttackPresses(
                        out ProjectWideAttackPressSnapshot held),
                    Is.True,
                    "held adapter read");
                Assert.That(held.AnyAttackPressed, Is.False);

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Assert.That(
                    adapter.TryReadAttackPresses(
                        out ProjectWideAttackPressSnapshot released),
                    Is.True,
                    "released adapter read");
                Assert.That(released.AnyAttackPressed, Is.False);

                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(
                    adapter.TryReadAttackPresses(
                        out ProjectWideAttackPressSnapshot pressedAgain),
                    Is.True,
                    "second press adapter read");
                Assert.That(
                    pressedAgain.PrimaryPressed,
                    Is.True,
                    "second primary edge");
                Assert.That(pressedAgain.SecondaryPressed, Is.False);
            }
            finally
            {
                adapter.Dispose();
                if (!sourceWasEnabled)
                {
                    source.Disable();
                }

                source.devices = previousDevices;
                InputSystem.actions = previousActions;
                InputSystem.settings = previousSettings;
                InputSystem.RemoveDevice(mouse);
                Object.DestroyImmediate(testSettings);
            }
        }


        private static void AssertBattleBinding(InputActionMap battle, string actionName, string path)
        {
            InputAction action = battle.FindAction(actionName, throwIfNotFound: true);
            Assert.That(action.type, Is.EqualTo(InputActionType.Button), actionName);
            Assert.That(action.expectedControlType, Is.EqualTo("Button"), actionName);
            Assert.That(action.interactions, Is.Empty, actionName);
            Assert.That(action.bindings.Count, Is.EqualTo(1), actionName);

            InputBinding binding = action.bindings[0];
            Assert.That(binding.path, Is.EqualTo(path), actionName);
            Assert.That(binding.groups, Is.EqualTo("Keyboard&Mouse"), actionName);
            Assert.That(binding.interactions, Is.Empty, actionName);
            Assert.That(binding.processors, Is.Empty, actionName);
            Assert.That(binding.isComposite, Is.False, actionName);
            Assert.That(binding.isPartOfComposite, Is.False, actionName);
        }

        private static void AssertHasBinding(InputAction action, string path)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (action.bindings[index].path == path)
                {
                    return;
                }
            }

            Assert.Fail(action.name + " is missing binding " + path + ".");
        }
    }
}
