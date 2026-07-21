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
    }
}
