using System;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0PlannerConfigurationValidatorTests
    {
        [Test]
        public void InstalledCombatLabPlannerConfigurationPassesReadOnlyPreflight()
        {
            Assert.That(
                TryValidateCombatLab(out string report),
                Is.True,
                report);
            Assert.That(report, Does.Contain("No assets or scenes were modified."));
            Assert.That(report, Does.Contain(
                "CombatLab: Fei vs Burstbug (combatlab-fei-vs-burstbug)"));
            Assert.That(report, Does.Contain(
                "Enemy: Burstbug; entry/patrol behavior, three reusable attack languages "
                + "and four death-state FX pools are valid."));
        }

        [Test]
        public void LegacyOnlyConfigurationFailsWithAnActionableMessage()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();
            try
            {
                Assert.That(
                    TryValidate(config, out string report),
                    Is.False);
                Assert.That(report, Does.Contain("authoredScenario"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static bool TryValidateCombatLab(out string report)
        {
            return InvokeValidator("TryValidateCombatLab", null, out report);
        }

        private static bool TryValidate(BattleScenarioConfig config, out string report)
        {
            return InvokeValidator("TryValidate", config, out report);
        }

        private static bool InvokeValidator(string methodName, object config, out string report)
        {
            Type validatorType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length && validatorType == null; index++)
            {
                validatorType = assemblies[index].GetType(
                    "FPG.Demo.Editor.D0PlannerConfigurationValidator");
            }

            Assert.That(validatorType, Is.Not.Null,
                "The D0 planner validation menu must be compiled into the Editor assembly.");
            MethodInfo method = config == null
                ? validatorType.GetMethod(methodName, new[] { typeof(string).MakeByRefType() })
                : validatorType.GetMethod(
                    methodName,
                    new[] { typeof(BattleScenarioConfig), typeof(string).MakeByRefType() });
            Assert.That(method, Is.Not.Null, $"Missing D0 planner validator method '{methodName}'.");

            object[] arguments = config == null
                ? new object[] { null }
                : new[] { config, null };
            bool isValid = (bool)method.Invoke(null, arguments);
            report = arguments[arguments.Length - 1] as string;
            return isValid;
        }
    }
}
