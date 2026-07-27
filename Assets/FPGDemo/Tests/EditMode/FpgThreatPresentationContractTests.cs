using System.Reflection;
using FPG.Demo.Enemy;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgThreatPresentationContractTests
    {
        [TestCase(FpgThreatPresentationKind.FastUninterceptable, false)]
        [TestCase(FpgThreatPresentationKind.InterceptableVolley, false)]
        [TestCase(FpgThreatPresentationKind.HeavyWeakpoint, true)]
        public void TypedBoundTargetActionUsesTimedImpactPresentationMatrix(
            FpgThreatPresentationKind presentationKind,
            bool expectedValid)
        {
            FpgSkillAttackEventDefinition action =
                new FpgSkillAttackEventDefinition();
            SetField(action, "mode", FpgSkillAttackMode.BoundTarget);
            SetField(action, "threatPresentationKind", presentationKind);

            Assert.That(
                TryValidateAction(action, out string error),
                Is.EqualTo(expectedValid),
                error);
        }

        [TestCase(false, FpgThreatPresentationKind.FastUninterceptable, true)]
        [TestCase(false, FpgThreatPresentationKind.InterceptableVolley, false)]
        [TestCase(false, FpgThreatPresentationKind.HeavyWeakpoint, false)]
        [TestCase(true, FpgThreatPresentationKind.FastUninterceptable, false)]
        [TestCase(true, FpgThreatPresentationKind.InterceptableVolley, true)]
        [TestCase(true, FpgThreatPresentationKind.HeavyWeakpoint, false)]
        public void TypedProjectileActionUsesInterceptabilityPresentationMatrix(
            bool interceptable,
            FpgThreatPresentationKind presentationKind,
            bool expectedValid)
        {
            FpgSkillProjectileEventDefinition action =
                new FpgSkillProjectileEventDefinition();
            SetField(action, "projectileInterceptable", interceptable);
            SetField(action, "projectileMaxHitPoints", interceptable ? 1 : 0);
            SetField(action, "threatPresentationKind", presentationKind);

            Assert.That(
                TryValidateAction(action, out string error),
                Is.EqualTo(expectedValid),
                error);
        }

        private static bool TryValidateAction(
            FpgSkillGameplayActionDefinition action,
            out string error)
        {
            MethodInfo method = action.GetType().GetMethod(
                "TryValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { 0, null };
            bool result = (bool)method.Invoke(action, arguments);
            error = arguments[1] as string ?? string.Empty;
            return result;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            for (System.Type type = target.GetType();
                type != null;
                type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    continue;
                }

                field.SetValue(target, value);
                return;
            }

            Assert.Fail(
                $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        }
    }
}
