using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PrototypeWeaponCombatHudEditorTests
{
    [Test]
    public void ResourcePipsMapNormalizedResourceToConfiguredCapacity()
    {
        MethodInfo resolver = RequireType("NewFPG.Combat.PrototypeWeaponCombatHud, Assembly-CSharp").GetMethod(
            "ResolveActiveResourcePipCount",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(float), typeof(int) },
            null);

        Assert.IsNotNull(resolver);
        Assert.That((int)resolver.Invoke(null, new object[] { 0f, 10 }), Is.EqualTo(0));
        Assert.That((int)resolver.Invoke(null, new object[] { 0.5f, 10 }), Is.EqualTo(5));
        Assert.That((int)resolver.Invoke(null, new object[] { 1f, 10 }), Is.EqualTo(10));
    }

    [Test]
    public void CombatHudDebugBootstrapKeepsResourceRefillOptIn()
    {
        GameObject bootstrapObject = new GameObject("Combat HUD Debug Bootstrap Test");
        try
        {
            Component resourcePool = bootstrapObject.AddComponent(
                RequireType("NewFPG.Combat.CombatResourcePool, Assembly-CSharp"));
            Component bootstrap = bootstrapObject.AddComponent(
                RequireType("NewFPG.Combat.CombatHudDebugBootstrap, Assembly-CSharp"));
            FieldInfo keepResourceFull = bootstrap.GetType().GetField(
                "keepResourceFull",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(keepResourceFull);
            Assert.That((bool)keepResourceFull.GetValue(bootstrap), Is.False);

            SetField(bootstrap, "resourcePool", resourcePool);
            Invoke(resourcePool, "SetCurrent", 4f);
            Invoke(bootstrap, "Bind");
            Assert.That(GetProperty<float>(resourcePool, "Current"), Is.EqualTo(4f).Within(0.0001f));

            keepResourceFull.SetValue(bootstrap, true);
            Invoke(resourcePool, "SetCurrent", 2f);
            Invoke(bootstrap, "Bind");
            Assert.That(GetProperty<float>(resourcePool, "Current"), Is.EqualTo(GetProperty<float>(resourcePool, "Max")).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(bootstrapObject);
        }
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, false);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static void Invoke(Component component, string methodName, params object[] arguments)
    {
        MethodInfo method = component.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(float) },
            null);
        if (method == null)
        {
            method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }

        Assert.IsNotNull(method, component.GetType().Name + "." + methodName + " should exist.");
        method.Invoke(component, arguments);
    }

    private static T GetProperty<T>(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, component.GetType().Name + "." + propertyName + " should exist.");
        return (T)property.GetValue(component);
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, component.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(component, value);
    }
}
