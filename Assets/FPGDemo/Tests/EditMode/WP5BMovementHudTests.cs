using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class WP5BMovementHudTests
    {
        [Test]
        public void ControllerDefaultsToFixedPlanarPositionInsteadOfUsingZeroMoveSpeed()
        {
            GameObject root = new GameObject("WP5BPlayerControllerTest");
            CombatLabPlayerController controller = root.AddComponent<CombatLabPlayerController>();
            try
            {
                SerializedObject serialized = new SerializedObject(controller);
                SerializedProperty planarMovementEnabled = serialized.FindProperty("planarMovementEnabled");
                SerializedProperty moveSpeed = serialized.FindProperty("moveSpeed");

                Assert.That(planarMovementEnabled, Is.Not.Null);
                Assert.That(moveSpeed, Is.Not.Null);
                Assert.That(controller.PlanarMovementEnabled, Is.False);
                Assert.That(planarMovementEnabled.boolValue, Is.False);
                Assert.That(moveSpeed.floatValue, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefreshUsesFrozenD0PromptWithoutWASD()
        {
            GameObject root = new GameObject("WP5BHudTest");
            GameObject promptObject = new GameObject(
                "PromptText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            promptObject.transform.SetParent(root.transform, false);

            BattleHudPresenter hud = root.AddComponent<BattleHudPresenter>();
            Text prompt = promptObject.GetComponent<Text>();
            try
            {
                SerializedObject serialized = new SerializedObject(hud);
                serialized.FindProperty("promptText").objectReferenceValue = prompt;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                ScenarioDefinition definition = CombatLabHarness.CreateScenario();
                FinalSnapshot snapshot = new FinalSnapshot(
                    BattleSessionState.Running,
                    BattleCompletionReason.None,
                    0L,
                    definition.PlayerLife,
                    definition.PlayerBarrier,
                    definition.PlayerWeapon.MagazineCapacity,
                    definition.EnemyLife,
                    definition.EnemyBreak,
                    EnemyControlState.Active,
                    0,
                    0);

                hud.Refresh(snapshot, definition);

                Assert.That(prompt.text, Is.EqualTo(BattleHudPresenter.PlaytestPrompt));
                Assert.That(prompt.text, Does.Not.Contain("WASD"));
                Assert.That(
                    prompt.text,
                    Is.EqualTo("RMB 瞄准、LMB 主射、E 蓄力/释放、R 换弹、Esc 暂停、F5 重开；快弹缩回、慢弹转火、重预警打弱点"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
