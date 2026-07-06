using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonsterCombatHudEditorTests
{
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();
    private readonly List<UnityEngine.Object> temporaryAssets = new List<UnityEngine.Object>();

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

        for (int i = temporaryAssets.Count - 1; i >= 0; i--)
        {
            if (temporaryAssets[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryAssets[i]);
            }
        }

        temporaryAssets.Clear();
    }

    [Test]
    public void DamageEventSpawnsDamageNumber()
    {
        Component hud = CreateHud(out Camera camera);
        Component combatant = CreateCombatant("Damage Number Enemy", out Component vitals);

        Invoke(hud, "SetTargetCamera", camera);
        Invoke(hud, "Track", combatant, false, "Fish");

        Invoke(vitals, "ReceiveDamage", CreateDamagePayload(12f, combatant.transform.position + Vector3.up));

        Assert.AreEqual(1, (int)GetProperty(hud, "ActiveDamageNumberCount"));
        Component view = FirstDamageNumberView((Component)hud);
        Assert.IsNotNull(view);
    }

    [Test]
    public void HealthBarRatioTracksCombatVitalsRatio()
    {
        Component hud = CreateHud(out Camera camera);
        Component combatant = CreateCombatant("Health Ratio Enemy", out Component vitals);

        Invoke(hud, "SetTargetCamera", camera);
        Invoke(hud, "Track", combatant, false, "Fish");
        Invoke(vitals, "ReceiveDamage", CreateDamagePayload(25f, combatant.transform.position));

        Component view = FirstHealthBarView((Component)hud, false);
        Assert.IsNotNull(view);
        Assert.AreEqual(0.75f, (float)GetProperty(view, "DisplayedHealthRatio"), 0.001f);
    }

    [Test]
    public void BossAndSmallMonsterUseSeparateBarModes()
    {
        Component hud = CreateHud(out Camera camera);
        Component small = CreateCombatant("Small Enemy", out _);
        Component boss = CreateCombatant("Boss Enemy", out _);

        Invoke(hud, "SetTargetCamera", camera);
        Invoke(hud, "Track", small, false, "Fish");
        Invoke(hud, "Track", boss, true, "Boss");

        Assert.AreEqual(2, (int)GetProperty(hud, "TrackedCount"));
        Assert.IsTrue((bool)GetProperty(hud, "BossHealthBarVisible"));
        Assert.IsNotNull(FirstHealthBarView((Component)hud, false));
        Assert.IsNotNull(FirstHealthBarView((Component)hud, true));
    }

    [Test]
    public void UntrackRemovesSubscriptionsAndStopsFutureDamageNumbers()
    {
        Component hud = CreateHud(out Camera camera);
        Component combatant = CreateCombatant("Untrack Enemy", out Component vitals);

        Invoke(hud, "SetTargetCamera", camera);
        Invoke(hud, "Track", combatant, false, "Fish");
        Invoke(hud, "Untrack", combatant);
        Invoke(vitals, "ReceiveDamage", CreateDamagePayload(10f, combatant.transform.position));

        Assert.AreEqual(0, (int)GetProperty(hud, "TrackedCount"));
        Assert.AreEqual(0, (int)GetProperty(hud, "ActiveDamageNumberCount"));
    }

    [Test]
    public void ClearRemovesSubscriptionsAndActiveNumbers()
    {
        Component hud = CreateHud(out Camera camera);
        Component combatant = CreateCombatant("Clear Enemy", out Component vitals);

        Invoke(hud, "SetTargetCamera", camera);
        Invoke(hud, "Track", combatant, false, "Fish");
        Invoke(vitals, "ReceiveDamage", CreateDamagePayload(10f, combatant.transform.position));
        Assert.AreEqual(1, (int)GetProperty(hud, "ActiveDamageNumberCount"));

        Invoke(hud, "Clear");
        Invoke(vitals, "ReceiveDamage", CreateDamagePayload(10f, combatant.transform.position));

        Assert.AreEqual(0, (int)GetProperty(hud, "TrackedCount"));
        Assert.AreEqual(0, (int)GetProperty(hud, "ActiveDamageNumberCount"));
    }

    [Test]
    public void RuntimeApiIsPublic()
    {
        Type hudType = RequireType("NewFPG.Combat.MonsterCombatHud, Assembly-CSharp");
        Type combatantType = RequireType("NewFPG.Level.LevelCombatant, Assembly-CSharp");
        Type healthBarType = RequireType("NewFPG.Combat.MonsterHealthBarView, Assembly-CSharp");
        Type damageNumberType = RequireType("NewFPG.Combat.DamageNumberView, Assembly-CSharp");
        Type vitalsType = RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp");
        Type hitTipRequestType = RequireType("NewFPG.Combat.HitTipRequest, Assembly-CSharp");
        Type hitTipStyleIdType = RequireType("NewFPG.Combat.HitTipStyleId, Assembly-CSharp");

        AssertPublicMethod(hudType, "Track", combatantType, typeof(bool), typeof(string));
        AssertPublicMethod(hudType, "Untrack", combatantType);
        AssertPublicMethod(hudType, "Clear");
        AssertPublicMethod(hudType, "ShowHitTip", hitTipRequestType);
        AssertPublicMethod(hudType, "ShowHitTip", typeof(float), typeof(Vector3), hitTipStyleIdType);
        AssertPublicMethod(healthBarType, "Bind", vitalsType, typeof(Transform), typeof(Camera), typeof(bool), typeof(string));
        AssertPublicMethod(damageNumberType, "Play", typeof(string), typeof(Vector2), typeof(Color), typeof(float));
    }

    [Test]
    public void ShowHitTipSpawnsConfiguredSpriteNumber()
    {
        Component hud = CreateHud(out Camera camera);
        Invoke(hud, "SetTargetCamera", camera);

        Type styleIdType = RequireType("NewFPG.Combat.HitTipStyleId, Assembly-CSharp");
        object critical = Enum.Parse(styleIdType, "Critical");
        Invoke(hud, "ShowHitTip", 987f, new Vector3(0f, 1f, 2f), critical);

        Assert.AreEqual(1, (int)GetProperty(hud, "ActiveDamageNumberCount"));
        Component view = FirstDamageNumberView((Component)hud);
        Assert.IsNotNull(view);
        Assert.Greater((float)GetProperty(view, "LastBackgroundWidth"), 0f);
        Assert.Greater((float)GetProperty(view, "LastDigitsWidth"), 0f);
    }

    [Test]
    public void DamageNumberBackgroundScalesWithDigitLength()
    {
        Component view = CreateDamageNumberView();
        object style = CreateRuntimeStyle();
        object animation = CreateRuntimeAnimation();
        Type requestType = RequireType("NewFPG.Combat.HitTipRequest, Assembly-CSharp");
        Type styleIdType = RequireType("NewFPG.Combat.HitTipStyleId, Assembly-CSharp");
        object normal = Enum.Parse(styleIdType, "Normal");
        ConstructorInfo requestCtor = requestType.GetConstructor(new[] { typeof(float), typeof(Vector3), styleIdType });
        Assert.IsNotNull(requestCtor);

        Invoke(view, "Play", requestCtor.Invoke(new[] { 1f, Vector3.zero, normal }), new Vector2(100f, 100f), style, animation);
        float oneDigitWidth = (float)GetProperty(view, "LastBackgroundWidth");
        Assert.GreaterOrEqual(oneDigitWidth, 60f);

        Invoke(view, "Play", requestCtor.Invoke(new[] { 123f, Vector3.zero, normal }), new Vector2(100f, 100f), style, animation);
        float threeDigitWidth = (float)GetProperty(view, "LastBackgroundWidth");

        Invoke(view, "Play", requestCtor.Invoke(new[] { 12345f, Vector3.zero, normal }), new Vector2(100f, 100f), style, animation);
        float fiveDigitWidth = (float)GetProperty(view, "LastBackgroundWidth");

        Assert.Greater(threeDigitWidth, oneDigitWidth);
        Assert.Greater(fiveDigitWidth, threeDigitWidth);
    }

    [Test]
    public void DamageNumberAnimationUsesScaleAndHighlightCurves()
    {
        Component view = CreateDamageNumberView();
        object style = CreateRuntimeStyle();
        object animation = CreateRuntimeAnimation();
        Type requestType = RequireType("NewFPG.Combat.HitTipRequest, Assembly-CSharp");
        Type styleIdType = RequireType("NewFPG.Combat.HitTipStyleId, Assembly-CSharp");
        object normal = Enum.Parse(styleIdType, "Normal");
        ConstructorInfo requestCtor = requestType.GetConstructor(new[] { typeof(float), typeof(Vector3), styleIdType });
        Assert.IsNotNull(requestCtor);

        Invoke(view, "Play", requestCtor.Invoke(new[] { 88f, Vector3.zero, normal }), new Vector2(100f, 100f), style, animation);
        Invoke(view, "Tick", 0.5f);

        Assert.AreEqual(1.5f, (float)GetProperty(view, "LastScale"), 0.001f);
        Assert.AreEqual(0.5f, (float)GetProperty(view, "LastHighlight"), 0.001f);
    }

    [Test]
    public void LegacyDamageNumberPlayStillAcceptsSignedText()
    {
        Component view = CreateDamageNumberView();
        Invoke(view, "Play", "-12", new Vector2(100f, 100f), Color.red, 0.5f);

        Text fallback = ((Component)view).GetComponentInChildren<Text>(true);
        Assert.IsNotNull(fallback);
        Assert.AreEqual("-12", fallback.text);
        Assert.AreEqual(Color.red, fallback.color);
    }

    [Test]
    public void DefaultHitTipCatalogContainsThreeCompleteStyles()
    {
        UnityEngine.Object catalog = Resources.Load("HitTips/SO_HTC_Default");
        Assert.IsNotNull(catalog);

        Type styleIdType = RequireType("NewFPG.Combat.HitTipStyleId, Assembly-CSharp");
        string[] styleNames = { "Normal", "Critical", "Elemental" };
        for (int i = 0; i < styleNames.Length; i++)
        {
            object styleId = Enum.Parse(styleIdType, styleNames[i]);
            object style = Invoke(catalog, "GetStyle", styleId);
            Assert.IsNotNull(style);
            Assert.IsTrue((bool)GetProperty(style, "IsValid"), styleNames[i] + " style should have background and digits.");
            for (int digit = 0; digit < 10; digit++)
            {
                Assert.IsNotNull(Invoke(style, "GetDigitSprite", digit));
            }
        }
    }

    private Component CreateHud(out Camera camera)
    {
        GameObject hudObject = CreateTrackedGameObject("Monster Combat Hud Test");
        Component hud = hudObject.AddComponent(RequireType("NewFPG.Combat.MonsterCombatHud, Assembly-CSharp"));
        InvokePrivate(hud, "Awake");

        GameObject cameraObject = CreateTrackedGameObject("Monster Combat Hud Camera");
        camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 3f, -8f);
        camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        return hud;
    }

    private Component CreateCombatant(string name, out Component vitals)
    {
        GameObject enemy = CreateTrackedGameObject(name);
        enemy.transform.position = new Vector3(0f, 0f, 2f);
        enemy.AddComponent<SpriteRenderer>();
        vitals = enemy.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        ApplyVitalsSettings(vitals, 100f, 0f);
        Invoke(vitals, "ResetVitals");
        Component combatant = enemy.AddComponent(RequireType("NewFPG.Level.LevelCombatant, Assembly-CSharp"));
        InvokePrivate(combatant, "Awake");
        return combatant;
    }

    private GameObject CreateTrackedGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        temporaryObjects.Add(gameObject);
        return gameObject;
    }

    private static object CreateDamagePayload(float amount, Vector3 hitPoint)
    {
        return Activator.CreateInstance(
            RequireType("NewFPG.Combat.DamagePayload, Assembly-CSharp"),
            new object[] { amount, null, hitPoint });
    }

    private static void ApplyVitalsSettings(Component vitals, float health, float shield)
    {
        object settings = Activator.CreateInstance(RequireType("NewFPG.Combat.CombatVitalsSettings, Assembly-CSharp"));
        SetField(settings, "maxHealth", health);
        SetField(settings, "startingHealth", health);
        SetField(settings, "maxShield", shield);
        SetField(settings, "startingShield", shield);
        SetField(settings, "destroyOnDeath", false);
        Invoke(vitals, "ApplySettings", settings, true);
    }

    private static Component FirstHealthBarView(Component hud, bool boss)
    {
        Component[] views = hud.GetComponentsInChildren(RequireType("NewFPG.Combat.MonsterHealthBarView, Assembly-CSharp"), true);
        for (int i = 0; i < views.Length; i++)
        {
            if ((bool)GetProperty(views[i], "IsBoss") == boss)
            {
                return views[i];
            }
        }

        return null;
    }

    private static Component FirstDamageNumberView(Component hud)
    {
        Component[] views = hud.GetComponentsInChildren(RequireType("NewFPG.Combat.DamageNumberView, Assembly-CSharp"), true);
        return views.Length > 0 ? views[0] : null;
    }

    private Component CreateDamageNumberView()
    {
        GameObject root = CreateTrackedGameObject("Damage Number View Test");
        root.AddComponent<RectTransform>();
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        temporaryObjects.Add(backgroundObject);
        backgroundObject.transform.SetParent(root.transform, false);
        Image background = backgroundObject.GetComponent<Image>();

        GameObject digitsObject = new GameObject("Digits", typeof(RectTransform));
        temporaryObjects.Add(digitsObject);
        digitsObject.transform.SetParent(root.transform, false);
        RectTransform digits = digitsObject.GetComponent<RectTransform>();

        GameObject fallbackObject = new GameObject("FallbackValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        temporaryObjects.Add(fallbackObject);
        fallbackObject.transform.SetParent(root.transform, false);
        Text fallback = fallbackObject.GetComponent<Text>();

        Component view = root.AddComponent(RequireType("NewFPG.Combat.DamageNumberView, Assembly-CSharp"));
        Invoke(view, "Initialize", background, digits, group, fallback);
        return view;
    }

    private object CreateRuntimeStyle()
    {
        Type styleConfigType = RequireType("NewFPG.Combat.HitTipStyleConfig, Assembly-CSharp");
        Type styleIdType = RequireType("NewFPG.Combat.HitTipStyleId, Assembly-CSharp");
        object style = Activator.CreateInstance(styleConfigType);
        Texture2D texture = new Texture2D(256, 64);
        temporaryAssets.Add(texture);
        Sprite background = Sprite.Create(texture, new Rect(0f, 0f, 133f, 50f), new Vector2(0.5f, 0.5f), 100f, 1, SpriteMeshType.FullRect, new Vector4(24f, 0f, 24f, 0f));
        temporaryAssets.Add(background);
        Sprite[] digits = new Sprite[10];
        for (int i = 0; i < digits.Length; i++)
        {
            float width = i == 1 ? 20f : 40f + i;
            digits[i] = Sprite.Create(texture, new Rect(0f, 0f, width, 60f), new Vector2(0.5f, 0.5f), 100f);
            temporaryAssets.Add(digits[i]);
        }

        Invoke(
            style,
            "Configure",
            Enum.Parse(styleIdType, "Normal"),
            background,
            digits,
            CreateRuntimeAnimation(),
            -2f,
            34f,
            new Vector2(60f, 50f),
            60f,
            Color.white,
            Color.yellow);
        return style;
    }

    private object CreateRuntimeAnimation()
    {
        Type animationType = RequireType("NewFPG.Combat.HitTipAnimationConfig, Assembly-CSharp");
        ScriptableObject animation = ScriptableObject.CreateInstance(animationType);
        temporaryAssets.Add(animation);
        SetField(animation, "lifetime", 1f);
        SetField(animation, "randomVerticalOffsetRange", Vector2.zero);
        SetField(animation, "randomHorizontalOffsetRange", Vector2.zero);
        SetField(animation, "verticalOffsetCurve", AnimationCurve.Linear(0f, 0f, 1f, 100f));
        SetField(animation, "scaleCurve", AnimationCurve.Linear(0f, 1f, 1f, 2f));
        SetField(animation, "highlightCurve", AnimationCurve.Linear(0f, 1f, 1f, 0f));
        return animation;
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static void AssertPublicMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        Assert.IsNotNull(
            type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null),
            type.Name + "." + methodName + " should be public.");
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = ResolveMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public, args);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = ResolveMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.NonPublic, args);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static MethodInfo ResolveMethod(Type type, string methodName, BindingFlags flags, object[] args)
    {
        MethodInfo[] methods = type.GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            bool matches = true;
            for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                object arg = args[parameterIndex];
                if (arg == null)
                {
                    continue;
                }

                Type parameterType = parameters[parameterIndex].ParameterType;
                if (!parameterType.IsInstanceOfType(arg))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        return null;
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        return property.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }
}
