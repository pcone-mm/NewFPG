using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class AssemblyBoundaryTests
    {
        private sealed class AssemblyDefinitionData
        {
            public string name;
            public string[] references;
            public string[] precompiledReferences;
            public string[] includePlatforms;
            public bool autoReferenced;
            public bool overrideReferences;
            public bool noEngineReferences;
        }

        [Test]
        public void AssemblyDefinitionsMatchTheWp1DependencyContract()
        {
            Dictionary<string, string[]> expectedReferences = new Dictionary<string, string[]>
            {
                { "Runtime/Core/FPG.Core.asmdef", Array.Empty<string>() },
                { "Runtime/Combat/FPG.Combat.asmdef", new[] { "FPG.Core" } },
                { "Runtime/Skills/FPG.Skills.asmdef", new[] { "FPG.Core" } },
                { "Runtime/Player/FPG.Player.asmdef", new[] { "FPG.Core", "FPG.Combat", "FPG.Skills" } },
                { "Runtime/Enemy/FPG.Enemy.asmdef", new[] { "FPG.Core", "FPG.Combat", "FPG.Skills" } },
                { "Runtime/Run/FPG.Run.asmdef", new[] { "FPG.Core", "FPG.Combat", "FPG.Player", "FPG.Enemy", "FPG.Skills" } },
                { "Runtime/Unity/FPG.Unity.asmdef", new[] { "FPG.Core", "FPG.Combat", "FPG.Player", "FPG.Enemy", "FPG.Run", "FPG.Skills", "spine-unity", "Unity.InputSystem", "Unity.ugui" } },
                { "Editor/LevelAuthoring/FPG.LevelAuthoring.Editor.asmdef", new[] { "FPG.Unity", "FPG.Run" } },
                { "Editor/SkillAuthoring/FPG.SkillAuthoring.Editor.asmdef", new[] { "FPG.Core", "FPG.Skills" } },
                // G3 CZN asset and fixed-pool tests inspect Spine types directly.
                // This remains an Editor-only, non-auto-referenced test edge;
                // no domain or runtime assembly may acquire this dependency.
                { "Tests/EditMode/FPG.EditMode.Tests.asmdef", new[] { "FPG.Core", "FPG.Combat", "FPG.Player", "FPG.Enemy", "FPG.Run", "FPG.Unity", "FPG.Skills", "FPG.LevelAuthoring.Editor", "FPG.SkillAuthoring.Editor", "spine-unity", "Unity.InputSystem" } },
                { "Tests/PlayMode/FPG.PlayMode.Tests.asmdef", new[] { "FPG.Unity", "FPG.Run", "FPG.Core", "FPG.Combat", "FPG.Enemy", "FPG.Player", "Unity.ugui" } }
            };

            string demoRoot = Path.Combine(Application.dataPath, "FPGDemo");
            string[] assemblyDefinitionFiles = Directory.GetFiles(demoRoot, "*.asmdef", SearchOption.AllDirectories);
            Assert.That(assemblyDefinitionFiles.Length, Is.EqualTo(11));

            foreach (KeyValuePair<string, string[]> expectation in expectedReferences)
            {
                string path = Path.Combine(demoRoot, expectation.Key.Replace('/', Path.DirectorySeparatorChar));
                AssemblyDefinitionData definition = JsonUtility.FromJson<AssemblyDefinitionData>(File.ReadAllText(path));
                string[] actualReferences = definition.references ?? Array.Empty<string>();

                CollectionAssert.AreEquivalent(expectation.Value, actualReferences, definition.name);
                Assert.That(definition.overrideReferences, Is.True, definition.name);
                Assert.That(Array.IndexOf(actualReferences, "Assembly-CSharp"), Is.EqualTo(-1), definition.name);
            }

            AssertDomainAssembly(demoRoot, "Runtime/Core/FPG.Core.asmdef");
            AssertDomainAssembly(demoRoot, "Runtime/Combat/FPG.Combat.asmdef");
            AssertDomainAssembly(demoRoot, "Runtime/Skills/FPG.Skills.asmdef");
            AssertDomainAssembly(demoRoot, "Runtime/Player/FPG.Player.asmdef");
            AssertDomainAssembly(demoRoot, "Runtime/Enemy/FPG.Enemy.asmdef");
            AssertDomainAssembly(demoRoot, "Runtime/Run/FPG.Run.asmdef");
            AssertEditorAssembly(demoRoot, "Editor/LevelAuthoring/FPG.LevelAuthoring.Editor.asmdef");
            AssertEditorAssembly(demoRoot, "Editor/SkillAuthoring/FPG.SkillAuthoring.Editor.asmdef");
            AssertTestAssembly(demoRoot, "Tests/EditMode/FPG.EditMode.Tests.asmdef");
            AssertTestAssembly(demoRoot, "Tests/PlayMode/FPG.PlayMode.Tests.asmdef");
        }

        private static void AssertDomainAssembly(string demoRoot, string relativePath)
        {
            AssemblyDefinitionData definition = ReadDefinition(demoRoot, relativePath);
            Assert.That(definition.noEngineReferences, Is.True, definition.name);
            Assert.That(definition.autoReferenced, Is.True, definition.name);
        }

        private static void AssertTestAssembly(string demoRoot, string relativePath)
        {
            AssemblyDefinitionData definition = ReadDefinition(demoRoot, relativePath);
            Assert.That(definition.autoReferenced, Is.False, definition.name);
            CollectionAssert.Contains(definition.precompiledReferences, "nunit.framework.dll", definition.name);
        }

        private static void AssertEditorAssembly(string demoRoot, string relativePath)
        {
            AssemblyDefinitionData definition = ReadDefinition(demoRoot, relativePath);
            CollectionAssert.AreEquivalent(new[] { "Editor" }, definition.includePlatforms);
            Assert.That(definition.autoReferenced, Is.True, definition.name);
            Assert.That(definition.noEngineReferences, Is.False, definition.name);
        }

        private static AssemblyDefinitionData ReadDefinition(string demoRoot, string relativePath)
        {
            string path = Path.Combine(demoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return JsonUtility.FromJson<AssemblyDefinitionData>(File.ReadAllText(path));
        }
    }
}
