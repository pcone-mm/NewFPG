using System.Linq;
using System.Reflection;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgBattleGmEditorWindowTests
    {
        [Test]
        public void UsesChineseEditorMenuWithoutShortcutInsteadOfRuntimePanel()
        {
            Assert.That(
                typeof(FpgBattleGmEditorWindow).IsSubclassOf(
                    typeof(EditorWindow)),
                Is.True);
            Assert.That(
                FpgBattleGmEditorWindow.WindowTitle,
                Is.EqualTo("战斗 GM 工具"));
            Assert.That(
                FpgBattleGmEditorWindow.MenuPath,
                Is.EqualTo("FPG Demo/战斗 GM 工具"));

            MethodInfo openMethod = typeof(FpgBattleGmEditorWindow).GetMethod(
                nameof(FpgBattleGmEditorWindow.Open),
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(openMethod, Is.Not.Null);
            MenuItem menuItem = openMethod
                .GetCustomAttributes(typeof(MenuItem), false)
                .Cast<MenuItem>()
                .Single();
            Assert.That(
                menuItem.menuItem,
                Is.EqualTo(FpgBattleGmEditorWindow.MenuPath));

            Assert.That(
                typeof(FpgBattleTestBootstrap).GetProperty("GmPanel"),
                Is.Null);
            Assert.That(
                typeof(FpgBattleTestBootstrap).Assembly.GetType(
                    "FPG.Demo.Unity.FpgBattleGmPanel"),
                Is.Null);
        }
    }
}
