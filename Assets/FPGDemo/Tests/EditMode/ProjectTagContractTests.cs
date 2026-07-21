using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;

namespace FPG.Demo.Tests.EditMode
{
    /// <summary>
    /// Keeps the global tag setup compatible with both the independent FPG demo
    /// and the legacy combat installer. Player is a Unity built-in tag and must
    /// never be added to the custom TagManager list.
    /// </summary>
    public sealed class ProjectTagContractTests
    {
        [Test]
        public void BuiltInPlayerTagIsAvailableWithoutCustomTagManagerDuplicate()
        {
            Assert.That(
                Array.IndexOf(InternalEditorUtility.tags, "Player"),
                Is.GreaterThanOrEqualTo(0),
                "Unity must expose its built-in Player tag.");

            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/TagManager.asset");
            Assert.That(tagManagerAssets, Is.Not.Empty);

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty customTags = tagManager.FindProperty("tags");
            Assert.That(customTags, Is.Not.Null);
            for (int index = 0; index < customTags.arraySize; index++)
            {
                Assert.That(
                    customTags.GetArrayElementAtIndex(index).stringValue,
                    Is.Not.EqualTo("Player"),
                    "Player is built in; duplicating it in TagManager.tags emits an editor warning.");
            }
        }
    }
}
