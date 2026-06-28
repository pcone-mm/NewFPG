using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HuxinjingShieldEffectEditorTests
{
    private const string PrefabPath = "Assets/Prefabs/Effects/PF_HuxinjingShield.prefab";
    private const float PositionTolerance = 0.00001f;

    private GameObject temporaryObject;

    [TearDown]
    public void TearDown()
    {
        if (temporaryObject != null)
        {
            UnityEngine.Object.DestroyImmediate(temporaryObject);
        }
    }

    [Test]
    public void PublicApiExposesPreviewControls()
    {
        Type type = RequireType("NewFPG.Combat.HuxinjingShieldEffect, Assembly-CSharp");

        AssertMethod(type, "PlayRelease");
        AssertMethod(type, "PlayHit", typeof(float));
        AssertMethod(type, "PlayDissolve");
        AssertMethod(type, "ResetVisualState");
        AssertMethod(type, "SetShieldRatio", typeof(float));
        AssertMethod(type, "SetShieldValues", typeof(float), typeof(float));
    }

    [Test]
    public void SetShieldRatioClampsValues()
    {
        Component effect = CreateEffect();

        Invoke(effect, "SetShieldRatio", 1.8f);
        Assert.AreEqual(1f, (float)GetProperty(effect, "ShieldRatio"), 0.0001f);

        Invoke(effect, "SetShieldRatio", -0.5f);
        Assert.AreEqual(0f, (float)GetProperty(effect, "ShieldRatio"), 0.0001f);
    }

    [Test]
    public void SetShieldValuesUsesCurrentOverMaximum()
    {
        Component effect = CreateEffect();

        Invoke(effect, "SetShieldValues", 25f, 100f);

        Assert.AreEqual(0.25f, (float)GetProperty(effect, "ShieldRatio"), 0.0001f);
    }

    [Test]
    public void GeneratedPrefabUsesLayerSprites()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, PrefabPath + " should exist.");

        Transform layer1 = prefab.transform.Find("huxinjing1");
        Transform layer2 = prefab.transform.Find("huxinjing2");
        Transform layer3 = prefab.transform.Find("huxinjing3");
        Assert.IsNotNull(layer1, "Prefab should render layer 1 as a child sprite.");
        Assert.IsNotNull(layer2, "Prefab should render layer 2 as a child sprite.");
        Assert.IsNotNull(layer3, "Prefab should render layer 3 as a child sprite.");
        AssertVector(Vector3.zero, layer1.localPosition, "Layer 1 should be centered from its PNG canvas.");
        AssertVector(Vector3.zero, layer2.localPosition, "Layer 2 should be centered from its PNG canvas.");
        AssertVector(Vector3.zero, layer3.localPosition, "Layer 3 should be centered from its PNG canvas.");

        SpriteRenderer renderer1 = layer1.GetComponent<SpriteRenderer>();
        SpriteRenderer renderer2 = layer2.GetComponent<SpriteRenderer>();
        SpriteRenderer renderer3 = layer3.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(renderer1, "Layer 1 child should have a SpriteRenderer.");
        Assert.IsNotNull(renderer2, "Layer 2 child should have a SpriteRenderer.");
        Assert.IsNotNull(renderer3, "Layer 3 child should have a SpriteRenderer.");
        Assert.IsNotNull(renderer1.sprite, "Layer 1 should reference huxinjing1.png.");
        Assert.IsNotNull(renderer2.sprite, "Layer 2 should reference huxinjing2.png.");
        Assert.IsNotNull(renderer3.sprite, "Layer 3 should reference huxinjing3.png.");
        Assert.AreEqual("huxinjing1", renderer1.sprite.name);
        Assert.AreEqual("huxinjing2", renderer2.sprite.name);
        Assert.AreEqual("huxinjing3", renderer3.sprite.name);
        Assert.AreEqual(2, renderer1.sortingOrder);
        Assert.AreEqual(1, renderer2.sortingOrder);
        Assert.AreEqual(0, renderer3.sortingOrder);

        Component effect = prefab.GetComponent(RequireType("NewFPG.Combat.HuxinjingShieldEffect, Assembly-CSharp"));
        SerializedObject serializedEffect = new SerializedObject(effect);
        Assert.IsNull(serializedEffect.FindProperty("compositeRenderer").objectReferenceValue);
        SerializedProperty layers = serializedEffect.FindProperty("layers");
        Assert.AreEqual(3, layers.arraySize);
        Assert.AreSame(layer1, layers.GetArrayElementAtIndex(0).FindPropertyRelative("layerTransform").objectReferenceValue);
        Assert.AreSame(renderer1, layers.GetArrayElementAtIndex(0).FindPropertyRelative("layerRenderer").objectReferenceValue);
        Assert.AreSame(layer2, layers.GetArrayElementAtIndex(1).FindPropertyRelative("layerTransform").objectReferenceValue);
        Assert.AreSame(renderer2, layers.GetArrayElementAtIndex(1).FindPropertyRelative("layerRenderer").objectReferenceValue);
        Assert.AreSame(layer3, layers.GetArrayElementAtIndex(2).FindPropertyRelative("layerTransform").objectReferenceValue);
        Assert.AreSame(renderer3, layers.GetArrayElementAtIndex(2).FindPropertyRelative("layerRenderer").objectReferenceValue);
    }

    private Component CreateEffect()
    {
        temporaryObject = new GameObject("Huxinjing Shield Effect Test");
        return temporaryObject.AddComponent(RequireType("NewFPG.Combat.HuxinjingShieldEffect, Assembly-CSharp"));
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static void AssertMethod(Type type, string name, params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
        Assert.IsNotNull(method, type.Name + "." + name + " should exist.");
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        return property.GetValue(target);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual, string message)
    {
        Assert.AreEqual(expected.x, actual.x, PositionTolerance, message + " x");
        Assert.AreEqual(expected.y, actual.y, PositionTolerance, message + " y");
        Assert.AreEqual(expected.z, actual.z, PositionTolerance, message + " z");
    }

}
