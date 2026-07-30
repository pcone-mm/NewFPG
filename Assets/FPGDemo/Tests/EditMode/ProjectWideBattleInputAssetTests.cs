using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class ProjectWideBattleInputAssetTests
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void BattleMapMatchesTheLegacyKeyboardAndMouseContract()
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
