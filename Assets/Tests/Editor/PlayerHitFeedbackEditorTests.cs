using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerHitFeedbackEditorTests
{
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
        {
            if (temporaryObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
            }
        }

        temporaryObjects.Clear();
        ClearCinemachineImpulses();
    }

    [Test]
    public void PlayFeedbackCreatesScreenEdgeOverlay()
    {
        Component feedback = CreateFeedback(out _);

        Invoke(feedback, "PlayFeedback", 1f);

        Canvas canvas = feedback.GetComponentInChildren<Canvas>(true);
        Assert.IsNotNull(canvas);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        Assert.GreaterOrEqual(canvas.sortingOrder, 500);

        CanvasGroup group = canvas.GetComponent<CanvasGroup>();
        Assert.IsNotNull(group);
        Assert.Greater(group.alpha, 0f);
        Assert.IsFalse(group.blocksRaycasts);

        Assert.IsNotNull(canvas.transform.Find("HitEdgeTop"));
        Assert.IsNotNull(canvas.transform.Find("HitEdgeBottom"));
        Assert.IsNotNull(canvas.transform.Find("HitEdgeLeft"));
        Assert.IsNotNull(canvas.transform.Find("HitEdgeRight"));
    }

    [Test]
    public void PlayFeedbackOffsetsAndRestoresCamera()
    {
        Component feedback = CreateFeedback(out Camera camera);
        Vector3 originalPosition = camera.transform.localPosition;

        SetField(feedback, "shakeDuration", 0.2f);
        SetField(feedback, "shakeAmplitude", 0.25f);
        Invoke(feedback, "PlayFeedback", 1f);

        InvokePrivate(feedback, "LateUpdate");
        Assert.Greater(Vector3.Distance(camera.transform.localPosition, originalPosition), 0.0001f);

        SetField(feedback, "shakeRemaining", 0f);
        InvokePrivate(feedback, "LateUpdate");
        Assert.Less(Vector3.Distance(camera.transform.localPosition, originalPosition), 0.0001f);
    }

    [Test]
    public void PlayFeedbackUsesCinemachineImpulseWhenCameraHasBrain()
    {
        Type brainType = RequireType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
        Type listenerType = RequireType("Unity.Cinemachine.CinemachineExternalImpulseListener, Unity.Cinemachine");
        Type sourceType = RequireType("Unity.Cinemachine.CinemachineImpulseSource, Unity.Cinemachine");

        Component feedback = CreateFeedback(out Camera camera);
        camera.gameObject.AddComponent(brainType);
        Vector3 originalPosition = camera.transform.localPosition;

        SetField(feedback, "shakeDuration", 0.2f);
        SetField(feedback, "shakeAmplitude", 0.25f);
        SetField(feedback, "impulseChannelMask", 1);
        SetField(feedback, "impulseGain", 1.25f);

        Invoke(feedback, "PlayFeedback", 1f);
        InvokePrivate(feedback, "LateUpdate");

        Component listener = camera.GetComponent(listenerType);
        Assert.IsNotNull(listener);
        Assert.AreNotEqual(0, (int)GetField(listener, "ChannelMask") & 1);
        Assert.GreaterOrEqual((float)GetField(listener, "Gain"), 1.25f);
        Assert.AreEqual(originalPosition, camera.transform.localPosition);

        Component source = feedback.GetComponent(sourceType);
        Assert.IsNotNull(source);
        object definition = GetField(source, "ImpulseDefinition");
        Assert.AreEqual(1, (int)GetField(definition, "ImpulseChannel"));
        Assert.AreEqual("Uniform", GetField(definition, "ImpulseType").ToString());
        Assert.IsTrue((bool)GetProperty(feedback, "IsFeedbackActive"));
    }

    [Test]
    public void CombatVitalsDamageTriggersFeedback()
    {
        Component feedback = CreateFeedback(out _);
        Component vitals = feedback.GetComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));

        Invoke(vitals, "ReceiveDamage", Activator.CreateInstance(
            RequireType("NewFPG.Combat.DamagePayload, Assembly-CSharp"),
            new object[] { 10f, null, Vector3.zero }));

        Assert.IsTrue((bool)GetProperty(feedback, "IsFeedbackActive"));
    }

    private Component CreateFeedback(out Camera camera)
    {
        GameObject player = CreateTrackedGameObject("Player Hit Feedback Test Player");
        Component vitals = player.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        SetField(vitals, "maxHealth", 100f);
        SetField(vitals, "startingHealth", 100f);
        SetField(vitals, "maxShield", 0f);
        SetField(vitals, "startingShield", 0f);
        Invoke(vitals, "ResetVitals");

        GameObject cameraObject = CreateTrackedGameObject("Player Hit Feedback Test Camera");
        camera = cameraObject.AddComponent<Camera>();
        camera.transform.localPosition = new Vector3(1f, 2f, -6f);

        Component feedback = player.AddComponent(RequireType("NewFPG.Combat.PlayerHitFeedback, Assembly-CSharp"));
        SetField(feedback, "vitals", vitals);
        SetField(feedback, "targetCamera", camera);
        InvokePrivate(feedback, "Awake");
        InvokePrivate(feedback, "OnEnable");
        return feedback;
    }

    private GameObject CreateTrackedGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        temporaryObjects.Add(gameObject);
        return gameObject;
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        return property.GetValue(target);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void ClearCinemachineImpulses()
    {
        Type managerType = Type.GetType("Unity.Cinemachine.CinemachineImpulseManager, Unity.Cinemachine", false);
        if (managerType == null)
        {
            return;
        }

        object manager = managerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
        MethodInfo clear = managerType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
        clear?.Invoke(manager, null);
    }
}
